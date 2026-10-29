using System.Text.RegularExpressions;
using Camion.Api.Contracts;
using Camion.Api.Inventario;
using Camion.Api.Models;

namespace Camion.Api.Suscripciones;

// P4p: reglas de planes y suscripciones (RN-PL-001, RN-PL-002, RN-SU-001 a RN-SU-005, D-35) y la comparación de
// nombres de catálogo (D-37). Lógica de negocio pura, separada de los endpoints y de la persistencia.
public static partial class ReglasP4p
{
    public const string MensajeRevisar = "Revisa los datos marcados.";
    public const string MensajeFiltros = "Revisa los filtros.";
    public const string MensajeNoEncontrado = "No encontrado.";
    public const string MensajeNombrePlan = "Escribe el nombre del plan (2 a 100 caracteres).";
    public const string MensajePrecioPlan = "Escribe el precio en pesos (número entero mayor o igual a 0).";
    public const string MensajeDuracion = "Escribe la duración en meses (número entero de 1 a 36).";
    public const string MensajeLimite = "Escribe el límite de publicaciones (número entero de 1 a 1000).";
    public const string MensajePlanDuplicado = "Ya existe un plan con ese nombre.";
    public const string MensajePlan = "Elige un plan.";
    public const string MensajePlanActivo = "Elige un plan activo.";
    public const string MensajeActivo = "Escribe true o false.";
    public const string MensajeEstado = "Estado no válido.";
    public const string MensajeAsesorId = "Asesor no válido.";
    public const string MensajePagina = "Página o tamaño no válidos.";
    public const string MensajeAsesorNoActivo = "Solo se puede asignar un plan a un asesor con la cuenta activa.";
    public const string MensajeYaTieneViva =
        "Este asesor ya tiene una suscripción activa, reactivada o suspendida. Renuévala o cámbiale el plan.";
    public const string MensajeSinSuscripcion = "Aún no tienes un plan activo. Comunícate con el administrador.";
    public const string MensajeNombreCatalogo = "Escribe el nombre (1 a 100 caracteres).";
    public const string MensajeTipoIds = "Elige al menos una categoría.";
    public const string MensajeTipoIdInvalido = "Categoría no válida.";
    public const string MensajeLineaSinCambios = "Escribe el nombre o elige las categorías.";

    public const int DiasPorMes = 30;         // D-35
    public const int DuracionMaxima = 36;
    public const int LimiteMaximo = 1000;
    public const decimal PrecioMaximo = 9_999_999_999_999m;   // DECIMAL(15,2)
    public const int MaxNombre = 100;

    /// <summary>D-35: cada mes del plan da 30 días.</summary>
    public static int DiasVigencia(int duracionMeses) => DiasPorMes * duracionMeses;

    /// <summary>ACTIVA o REACTIVADA: los estados que cuentan como vigentes si la fecha lo permite.</summary>
    public static bool EstadoVigente(string estado) => estado is Suscripcion.Activa or Suscripcion.Reactivada;

    public static bool EsViva(string estado) => Suscripcion.EstadosVivos.Contains(estado);

    /// <summary>Vigente = ACTIVA o REACTIVADA, inicio &lt;= ahora y fin &gt; ahora.</summary>
    public static bool Vigente(Suscripcion s, DateTime ahora) =>
        EstadoVigente(s.Estado) && s.InicioVigencia is { } inicio && inicio <= ahora && s.FinVigencia is { } fin && fin > ahora;

    /// <summary>Vencida calculada al leer: ACTIVA/REACTIVADA con fin &lt;= ahora (o la columna ya dice VENCIDA).</summary>
    public static bool Vencida(Suscripcion s, DateTime ahora) =>
        s.Estado == Suscripcion.Vencida || (EstadoVigente(s.Estado) && s.FinVigencia is { } fin && fin <= ahora);

    public static string EstadoEfectivo(Suscripcion s, DateTime ahora) => Vencida(s, ahora) ? Suscripcion.Vencida : s.Estado;

    /// <summary>Días que faltan hasta el fin (redondeo hacia arriba, nunca negativo); null si no hay fecha de fin.</summary>
    public static int? DiasRestantes(Suscripcion s, DateTime ahora)
    {
        if (s.FinVigencia is not { } fin) return null;
        var dias = (fin - ahora).TotalDays;
        return dias <= 0 ? 0 : (int)Math.Ceiling(dias);
    }

    /// <summary>Texto del estado en el 409: "No se puede renovar una suscripción en estado suspendida."</summary>
    public static string Mostrar(string estado) => estado.ToLowerInvariant();

    public static string MensajeTransicion(string accion, string estadoEfectivo) =>
        $"No se puede {accion} una suscripción en estado {Mostrar(estadoEfectivo)}.";

    /// <summary>
    /// CK_Sus_Fechas exige fin &gt; inicio. Al terminar una suscripción en el mismo instante en que empezó, el fin se
    /// deja 1 microsegundo después del inicio (la precisión de DATETIME(6)).
    /// </summary>
    public static DateTime FinAlTerminar(Suscripcion s, DateTime ahora)
    {
        var fin = s.FinVigencia is { } actual && actual < ahora ? actual : ahora;
        return s.InicioVigencia is { } inicio && fin <= inicio ? inicio.AddTicks(10) : fin;
    }

    // ---------------------------------------------------------------- planes

    public static string NormalizarTexto(string? valor) => EspaciosRepetidos().Replace((valor ?? "").Trim(), " ");

    /// <summary>Nombre de plan único sin distinguir mayúsculas.</summary>
    public static string ClavePlan(string nombre) => NormalizarTexto(nombre).ToUpperInvariant();

    public static Dictionary<string, string[]> ValidarPlan(PlanRequest? r)
    {
        var errores = new Dictionary<string, string[]>();
        var nombre = NormalizarTexto(r?.Nombre);
        if (nombre.Length is < 2 or > MaxNombre) errores["nombre"] = [MensajeNombrePlan];
        if (r?.PrecioCop is not { } precio || precio < 0 || precio > PrecioMaximo || decimal.Truncate(precio) != precio)
            errores["precioCop"] = [MensajePrecioPlan];
        if (r?.DuracionMeses is not { } meses || meses is < 1 or > DuracionMaxima) errores["duracionMeses"] = [MensajeDuracion];
        if (r?.LimitePublicaciones is not { } limite || limite is < 1 or > LimiteMaximo)
            errores["limitePublicaciones"] = [MensajeLimite];
        return errores;
    }

    // ---------------------------------------------------------------- catálogos

    /// <summary>D-37: clave de unicidad sin mayúsculas ni tildes ni espacios repetidos (se compara en C#, igual en SQLite y MariaDB).</summary>
    public static string ClaveCatalogo(string nombre) => ReglasInventario.SinTildes(NormalizarTexto(nombre)).ToUpperInvariant();

    public static Dictionary<string, string[]> ValidarNombreCatalogo(string? nombre)
    {
        var errores = new Dictionary<string, string[]>();
        var limpio = NormalizarTexto(nombre);
        if (limpio.Length is < 1 or > MaxNombre) errores["nombre"] = [MensajeNombreCatalogo];
        return errores;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex EspaciosRepetidos();
}
