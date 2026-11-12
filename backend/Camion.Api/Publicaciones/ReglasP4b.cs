using System.Globalization;
using System.Text.RegularExpressions;
using Camion.Api.Contracts;
using Camion.Api.Inventario;
using Camion.Api.Models;

namespace Camion.Api.Publicaciones;

// P4b: reglas de publicaciones (HU-023 a HU-028, HU-065), destacados (D-38), venta (HU-064, D-22, D-39) y reasignación
// (D-34). Lógica de negocio pura, sin acceso a la base. Contrato: docs/criterios/P4b-contrato-api.md.
public static partial class ReglasP4b
{
    // ---------------------------------------------------------------- estados del contrato

    // Estado que se informa: los de la columna más VENDIDA (estado comercial VENDIDO) y, en estadoEfectivo, VENCIDA.
    public const string Vendida = "VENDIDA";
    public const string Vencida = "VENCIDA";

    public static readonly string[] EstadosFiltro =
        [Publicacion.Publicada, Publicacion.Oculta, Vendida, Publicacion.Eliminada, Vencida];

    // Motivos de historial_vigencias (VARCHAR(30)).
    public const string MotivoPublicar = "PUBLICAR";
    public const string MotivoReactivar = "REACTIVAR";
    public const string MotivoRenovar = "RENOVAR";
    public const string MotivoReasignar = "REASIGNAR";

    // ---------------------------------------------------------------- mensajes

    public const string MensajeRevisar = "Revisa los datos marcados.";
    public const string MensajeFiltros = "Revisa los filtros.";
    public const string MensajeNoEncontrada = "Publicación no encontrada.";
    public const string MensajeVehiculoNoEncontrado = "Vehículo no encontrado.";
    public const string MensajeSinPlan = "No tienes un plan activo. Comunícate con el administrador.";
    public const string MensajeVehiculoId = "Elige un vehículo.";
    public const string MensajeTitulo = "Escribe el título (5 a 180 caracteres).";
    public const string MensajeDescripcion = "La descripción no puede tener más de 4000 caracteres.";
    public const string MensajeYaPublicado = "Este vehículo ya tiene una publicación.";
    public const string MensajeSinFoto = "Agrega al menos una foto del vehículo antes de publicarlo.";
    public const string MensajeSinPrecio = "Escribe el precio del vehículo antes de publicarlo.";
    public const string MensajeEstado = "Estado no válido.";
    public const string MensajeAsesorId = "Asesor no válido.";
    public const string MensajeAsesorCupo = "Elige un asesor.";
    public const string MensajeBuscar = "La búsqueda no puede tener más de 100 caracteres.";
    public const string MensajePagina = "Página o tamaño no válidos.";

    // Venta (HU-064, D-39).
    public const string MensajeFechaVenta = "Escribe la fecha de la venta (AAAA-MM-DD), de hoy o anterior.";
    public const string MensajeFechaAntesDelRegistro = "La fecha de la venta no puede ser anterior al registro del vehículo.";
    public const string MensajePrecioFinal = "Escribe el precio final en pesos (número entero mayor que 0).";
    public const string MensajeOrigen = "Elige el origen de la venta.";
    public const string MensajeObservaciones = "Las observaciones no pueden tener más de 1000 caracteres.";
    public const string MensajeObservacionesOtro = "Escribe las observaciones: son obligatorias si el origen es Otro.";

    // Reasignación (D-34).
    public const string MensajeMismoAsesor = "El vehículo ya pertenece a ese asesor.";
    public const string MensajeDestinoSinPlan =
        "El asesor destino no tiene un plan activo: no tiene cupo para la publicación de este vehículo.";

    public static string MensajeCupo(int limite) =>
        $"Ya usas las {limite} publicaciones de tu plan. Elimina una para crear otra.";

    public static string MensajeDestacados(int limite) =>
        $"Tu plan permite {limite} destacado(s). Quita uno para destacar otra.";

    public static string MensajeDestinoSinCupo(int limite) =>
        $"El asesor destino ya usa las {limite} publicaciones de su plan.";

    /// <summary>409 de transición: "No se puede inactivar una publicación en estado vencida."</summary>
    public static string MensajeTransicion(string accion, string estadoEfectivo) =>
        $"No se puede {accion} una publicación en estado {estadoEfectivo.ToLowerInvariant()}.";

    public static string MensajeVehiculoNoPublicable(string estadoVehiculo) =>
        $"No se puede publicar un vehículo en estado {estadoVehiculo.ToLowerInvariant()}.";

    public static string MensajeVehiculoTransicion(string accion, string estadoVehiculo) =>
        $"No se puede {accion} un vehículo en estado {estadoVehiculo.ToLowerInvariant()}.";

    // ---------------------------------------------------------------- límites

    public const int TituloMinimo = 5;
    public const int TituloMaximo = 180;
    public const int DescripcionMaxima = 4000;
    public const int ObservacionesMaximas = 1000;
    public const int MaxBusqueda = 100;
    public const decimal PrecioMaximo = 9_999_999_999_999m;   // DECIMAL(15,2)

    // Colombia no tiene horario de verano: la fecha "de hoy" de una venta se calcula en UTC-5.
    private static readonly TimeSpan DesfaseColombia = TimeSpan.FromHours(-5);

    // ---------------------------------------------------------------- estados

    /// <summary>Estado del contrato: ELIMINADA, VENDIDA (estado comercial VENDIDO) o la columna estado_publicacion.</summary>
    public static string Estado(string estadoPublicacion, string estadoComercial) =>
        estadoPublicacion == Publicacion.Eliminada ? Publicacion.Eliminada
        : estadoComercial == Publicacion.Vendido ? Vendida
        : estadoPublicacion;

    /// <summary>D-41: una PUBLICADA sin plan vigente del asesor (o sin vigencia iniciada) está VENCIDA.</summary>
    public static string EstadoEfectivo(string estado, bool vigente) =>
        estado == Publicacion.Publicada && !vigente ? Vencida : estado;

    public static bool VehiculoPublicable(string estadoVehiculo) =>
        estadoVehiculo is Vehiculo.Activo or Vehiculo.Reservado;

    // ---------------------------------------------------------------- validación

    public static string NormalizarTexto(string? valor) => EspaciosRepetidos().Replace((valor ?? "").Trim(), " ");

    /// <summary>Título (5 a 180) y descripción comercial (hasta 4000). Devuelve los valores normalizados.</summary>
    public static Dictionary<string, string[]> ValidarTextos(string? titulo, string? descripcion,
        out string tituloLimpio, out string? descripcionLimpia)
    {
        var errores = new Dictionary<string, string[]>();
        tituloLimpio = NormalizarTexto(titulo);
        if (tituloLimpio.Length is < TituloMinimo or > TituloMaximo) errores["titulo"] = [MensajeTitulo];
        var d = (descripcion ?? "").Trim();
        descripcionLimpia = d.Length == 0 ? null : d;
        if (d.Length > DescripcionMaxima) errores["descripcionComercial"] = [MensajeDescripcion];
        return errores;
    }

    public static DateOnly HoyColombia(DateTime ahoraUtc) => DateOnly.FromDateTime(ahoraUtc.Add(DesfaseColombia));

    /// <summary>
    /// HU-064, D-39: fecha (AAAA-MM-DD, hoy o anterior en Colombia y no anterior al registro del vehículo), precio final
    /// entero &gt; 0, origen de la lista fija y observaciones (hasta 1000; obligatorias si el origen es OTRO).
    /// </summary>
    public static Dictionary<string, string[]> ValidarVenta(VentaRequest? r, DateTime ahoraUtc, DateTime vehiculoCreadoEnUtc,
        out DatosVenta? datos)
    {
        var errores = new Dictionary<string, string[]>();
        datos = null;
        DateOnly fecha = default;
        if (string.IsNullOrWhiteSpace(r?.FechaVenta) ||
            !DateOnly.TryParseExact(r.FechaVenta.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha) ||
            fecha > HoyColombia(ahoraUtc))
            errores["fechaVenta"] = [MensajeFechaVenta];
        else if (fecha < HoyColombia(vehiculoCreadoEnUtc))
            errores["fechaVenta"] = [MensajeFechaAntesDelRegistro];

        if (r?.PrecioFinalCop is not { } precio || precio <= 0 || precio > PrecioMaximo || decimal.Truncate(precio) != precio)
            errores["precioFinalCop"] = [MensajePrecioFinal];

        var origen = (r?.Origen ?? "").Trim().ToUpperInvariant();
        if (!VentaVehiculo.Origenes.Contains(origen)) errores["origen"] = [MensajeOrigen];

        var obs = (r?.Observaciones ?? "").Trim();
        if (obs.Length > ObservacionesMaximas) errores["observaciones"] = [MensajeObservaciones];
        else if (obs.Length == 0 && origen == VentaVehiculo.OrigenOtro) errores["observaciones"] = [MensajeObservacionesOtro];

        if (errores.Count == 0)
            datos = new DatosVenta(fecha, r!.PrecioFinalCop!.Value, origen, obs.Length == 0 ? null : obs);
        return errores;
    }

    // ---------------------------------------------------------------- slug

    /// <summary>
    /// slug_publico a partir de la marca, el modelo y el año (o la categoría si no hay marca ni modelo) y el id, que lo
    /// hace único: "chevrolet-npr-2020-15".
    /// </summary>
    public static string Slug(string? marca, string? linea, int? anio, string tipo, long id)
    {
        var partes = new[] { marca, linea, anio?.ToString(CultureInfo.InvariantCulture) }
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (partes.Count == 0 || (marca is null && linea is null)) partes.Insert(0, tipo);
        var texto = ReglasInventario.SinTildes(string.Join(" ", partes!)).ToLowerInvariant();
        var baseSlug = NoAlfanumerico().Replace(texto, "-").Trim('-');
        if (baseSlug.Length > 150) baseSlug = baseSlug[..150].Trim('-');
        if (baseSlug.Length == 0) baseSlug = "vehiculo";
        return $"{baseSlug}-{id.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>"Chevrolet NPR 2020" o, sin marca ni modelo, la categoría.</summary>
    public static string TituloVehiculo(string? marca, string? linea, int? anio, string tipo)
    {
        var partes = new[] { marca, linea, anio?.ToString(CultureInfo.InvariantCulture) }
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (marca is null && linea is null) partes.Insert(0, tipo);
        return string.Join(" ", partes!);
    }

    /// <summary>Fin de un destacado o vigencia que se cierra "ahora" sin violar CHECK (fin &gt; inicio): 1 µs después del inicio.</summary>
    public static DateTime FinAlCerrar(DateTime inicio, DateTime fin, DateTime ahora)
    {
        var nuevo = fin < ahora ? fin : ahora;
        return nuevo <= inicio ? inicio.AddTicks(10) : nuevo;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex EspaciosRepetidos();

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex NoAlfanumerico();
}

public sealed record DatosVenta(DateOnly Fecha, decimal PrecioFinalCop, string Origen, string? Observaciones);
