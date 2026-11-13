using System.Globalization;
using Camion.Api.Data;
using Camion.Api.Leads;
using Camion.Api.Models;
using Camion.Api.Publicaciones;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Reportes;

// P8 (CA-P8-02 a CA-P8-05, D-68 A): mensajes de validación de los filtros de reportes e indicadores.
public static class MensajesP8
{
    public const string Filtros = "Revisa los filtros.";
    public const string Fecha = "Escribe una fecha válida (aaaa-mm-dd).";
    public const string Rango = "La fecha inicial no puede ser posterior a la final.";
    public const string PeriodoMaximo = "El período no puede superar 366 días.";
    public const string Asesor = "Asesor no válido.";
    public const string Categoria = "Categoría no válida.";
    public const string Departamento = "Departamento no válido.";
    public const string Municipio = "Municipio no válido.";
    public const string MunicipioDepartamento = "El municipio no corresponde al departamento.";
    public const string Origen = "Origen no válido.";
    public const string Estado = "Estado no válido.";
    public const string Resultado = "Resultado no válido.";
    public const string ResultadoOrigen = "Ese resultado no corresponde al origen elegido.";
    public const string Responsable = "Responsable no válido.";
    public const string Plan = "Plan no válido.";
    public const string Medio = "Medio no válido.";
    public const string Canal = "Canal no válido.";
    public const string Pagina = "Página o tamaño no válidos.";
    public const string DemasiadasFilas = "El reporte tiene demasiadas filas. Acorta el período o añade filtros.";
    public const string AvisoPagos = "Pagos registrados a mano por la administración. No es contabilidad ni facturación.";
    public const string VehiculoNoEncontrado = "Vehículo no encontrado.";
    public const string SuscripcionNoEncontrada = "Suscripción no encontrada.";
}

/// <summary>Qué filtros propios lee cada reporte (los demás parámetros se ignoran).</summary>
[Flags]
public enum UsaFiltros
{
    Ninguno = 0,
    Asesor = 1,
    Categoria = 2,
    Ubicacion = 4,
    OrigenLead = 8,
    EstadoLead = 16,
    Resultado = 32,
    Responsable = 64,
    EstadoCredito = 128,
    Canal = 256,
    OrigenVenta = 512,
    Plan = 1024,
    Medio = 2048,
    Paginacion = 4096
}

/// <summary>Filtros ya validados. Las fechas son días de Colombia; InicioUtc/FinUtc es el intervalo [inicio, fin) en UTC.</summary>
public sealed class FiltrosReporte
{
    public const string CanalPortada = "PORTADA";
    public const string CanalFicha = "FICHA";
    public static readonly string[] Canales = [CanalPortada, CanalFicha];
    public static readonly string[] EstadosCredito =
        [ReglasLeads.EstadoRegistrado, ReglasLeads.EstadoEnGestion, ReglasLeads.EstadoCerradoGestion];

    public const int TamanoPorDefecto = 20;
    public const int TamanoMaximo = 100;
    public const int DiasMaximos = 366;

    // Colombia: UTC-5 sin horario de verano (igual que LeadsEndpoints y ReglasP4b).
    public static readonly TimeSpan DesfaseColombia = TimeSpan.FromHours(5);

    public DateOnly Desde { get; init; }
    public DateOnly Hasta { get; init; }
    public DateTime InicioUtc => InicioDe(Desde);
    public DateTime FinUtc => InicioDe(Hasta.AddDays(1));
    public long? AsesorId { get; init; }
    public long? CategoriaId { get; init; }
    public long? DepartamentoId { get; init; }
    public long? MunicipioId { get; init; }
    public string? Origen { get; init; }
    public string? Estado { get; init; }
    public string? Resultado { get; init; }
    public bool SoloAdministracion { get; init; }
    public long? ResponsableAsesorId { get; init; }
    public string? Canal { get; init; }
    public long? PlanId { get; init; }
    public string? Medio { get; init; }
    public int Pagina { get; init; } = 1;
    public int Tamano { get; init; } = TamanoPorDefecto;

    public static DateTime InicioDe(DateOnly dia) => dia.ToDateTime(TimeOnly.MinValue) + DesfaseColombia;

    public static DateOnly DiaColombia(DateTime utc) => DateOnly.FromDateTime(utc - DesfaseColombia);

    /// <summary>Mismo período, desplazado: el anterior de igual duración que termina el día antes de Desde (CA-P8-35).</summary>
    public FiltrosReporte PeriodoAnterior()
    {
        var dias = Hasta.DayNumber - Desde.DayNumber + 1;
        return new FiltrosReporte { Desde = Desde.AddDays(-dias), Hasta = Desde.AddDays(-1) };
    }

    /// <summary>CA-P8-46: filtros aplicados, para la auditoría de la exportación (solo los que tienen valor).</summary>
    public Dictionary<string, object?> ParaAuditoria()
    {
        var d = new Dictionary<string, object?>
        {
            ["desde"] = Desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["hasta"] = Hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };
        void Poner(string clave, object? valor) { if (valor is not null) d[clave] = valor; }
        Poner("asesorId", AsesorId);
        Poner("categoriaId", CategoriaId);
        Poner("departamentoId", DepartamentoId);
        Poner("municipioId", MunicipioId);
        Poner("origen", Origen);
        Poner("estado", Estado);
        Poner("resultado", Resultado);
        Poner("responsable", SoloAdministracion ? ReglasLeads.ResponsableAdministracion : ResponsableAsesorId);
        Poner("canal", Canal);
        Poner("planId", PlanId);
        Poner("medio", Medio);
        return d;
    }

    /// <summary>
    /// Lee y valida los filtros de la petición (CA-P8-04). Sin fechas: el mes en curso (CA-P8-02). Devuelve null y los
    /// errores si algo no es válido.
    /// </summary>
    public static async Task<(FiltrosReporte? Filtros, Dictionary<string, string[]> Errores)> LeerAsync(
        IQueryCollection q, CamionDbContext db, DateTime ahoraUtc, UsaFiltros usa, CancellationToken ct,
        string? origenFijo = null)
    {
        var errores = new Dictionary<string, string[]>();
        var hoy = ReglasP4b.HoyColombia(ahoraUtc);
        var inicioMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var desde = LeerFecha(q, "desde", errores) ?? inicioMes;
        var hasta = LeerFecha(q, "hasta", errores) ?? inicioMes.AddMonths(1).AddDays(-1);
        if (!errores.ContainsKey("desde") && !errores.ContainsKey("hasta"))
        {
            if (desde > hasta) errores["hasta"] = [MensajesP8.Rango];
            else if (hasta.DayNumber - desde.DayNumber + 1 > DiasMaximos) errores["hasta"] = [MensajesP8.PeriodoMaximo];
        }

        long? asesor = null, categoria = null, departamento = null, municipio = null, plan = null, responsableId = null;
        string? origen = origenFijo, estado = null, resultado = null, canal = null, medio = null;
        var soloAdmin = false;

        if (usa.HasFlag(UsaFiltros.Asesor))
        {
            asesor = LeerEntero(q, "asesorId", MensajesP8.Asesor, errores);
            if (asesor is { } a && !await db.PerfilesAsesor.AsNoTracking().AnyAsync(p => p.Id == a, ct))
                errores["asesorId"] = [MensajesP8.Asesor];
        }
        if (usa.HasFlag(UsaFiltros.Categoria))
        {
            categoria = LeerEntero(q, "categoriaId", MensajesP8.Categoria, errores);
            if (categoria is { } c && !await db.TiposVehiculo.AsNoTracking().AnyAsync(t => t.Id == c, ct))
                errores["categoriaId"] = [MensajesP8.Categoria];
        }
        if (usa.HasFlag(UsaFiltros.Ubicacion))
        {
            departamento = LeerEntero(q, "departamentoId", MensajesP8.Departamento, errores);
            if (departamento is { } dep && !await db.Departamentos.AsNoTracking().AnyAsync(x => x.Id == dep, ct))
                errores["departamentoId"] = [MensajesP8.Departamento];
            municipio = LeerEntero(q, "municipioId", MensajesP8.Municipio, errores);
            if (municipio is { } mun)
            {
                var depDelMunicipio = await db.Municipios.AsNoTracking().Where(m => m.Id == mun)
                    .Select(m => (long?)m.DepartamentoId).FirstOrDefaultAsync(ct);
                if (depDelMunicipio is null) errores["municipioId"] = [MensajesP8.Municipio];
                else if (departamento is { } d2 && !errores.ContainsKey("departamentoId") && depDelMunicipio != d2)
                    errores["municipioId"] = [MensajesP8.MunicipioDepartamento];
            }
        }
        if (usa.HasFlag(UsaFiltros.OrigenLead))
        {
            origen = Codigo(q, "origen");
            if (origen is not null && !Lead.Origenes.Contains(origen)) errores["origen"] = [MensajesP8.Origen];
        }
        if (usa.HasFlag(UsaFiltros.OrigenVenta))
        {
            origen = Codigo(q, "origen");
            if (origen is not null && !VentaVehiculo.Origenes.Contains(origen)) errores["origen"] = [MensajesP8.Origen];
        }
        if (usa.HasFlag(UsaFiltros.EstadoLead))
        {
            estado = Codigo(q, "estado");
            if (estado is not null && !Lead.Estados.Contains(estado)) errores["estado"] = [MensajesP8.Estado];
        }
        if (usa.HasFlag(UsaFiltros.EstadoCredito))
        {
            estado = Codigo(q, "estado");
            if (estado is not null && !EstadosCredito.Contains(estado)) errores["estado"] = [MensajesP8.Estado];
        }
        if (usa.HasFlag(UsaFiltros.Resultado))
        {
            resultado = Codigo(q, "resultado");
            if (resultado is not null)
            {
                if (!Lead.Resultados.Contains(resultado)) errores["resultado"] = [MensajesP8.Resultado];
                else if (origen is not null && Lead.Origenes.Contains(origen) && !Lead.ResultadosDe(origen).Contains(resultado))
                    errores["resultado"] = [MensajesP8.ResultadoOrigen];
            }
        }
        if (usa.HasFlag(UsaFiltros.Responsable))
        {
            var texto = Texto(q, "responsable");
            if (texto is not null)
            {
                if (texto.Equals(ReglasLeads.ResponsableAdministracion, StringComparison.OrdinalIgnoreCase)) soloAdmin = true;
                else
                {
                    responsableId = LeerEntero(q, "responsable", MensajesP8.Responsable, errores);
                    if (responsableId is { } r && !await db.PerfilesAsesor.AsNoTracking().AnyAsync(p => p.Id == r, ct))
                        errores["responsable"] = [MensajesP8.Responsable];
                }
            }
        }
        if (usa.HasFlag(UsaFiltros.Canal))
        {
            canal = Codigo(q, "canal");
            if (canal is not null && !Canales.Contains(canal)) errores["canal"] = [MensajesP8.Canal];
        }
        if (usa.HasFlag(UsaFiltros.Plan))
        {
            plan = LeerEntero(q, "planId", MensajesP8.Plan, errores);
            if (plan is { } p && !await db.Planes.AsNoTracking().AnyAsync(x => x.Id == p, ct))
                errores["planId"] = [MensajesP8.Plan];
        }
        if (usa.HasFlag(UsaFiltros.Medio))
        {
            medio = Codigo(q, "medio");
            if (medio is not null && !Pago.Medios.Contains(medio)) errores["medio"] = [MensajesP8.Medio];
        }

        var pagina = 1;
        var tamano = TamanoPorDefecto;
        if (usa.HasFlag(UsaFiltros.Paginacion))
        {
            pagina = (int)(LeerEntero(q, "pagina", MensajesP8.Pagina, errores, int.MaxValue) ?? 1);
            tamano = (int)(LeerEntero(q, "tamano", MensajesP8.Pagina, errores, TamanoMaximo) ?? TamanoPorDefecto);
        }

        if (errores.Count > 0) return (null, errores);
        return (new FiltrosReporte
        {
            Desde = desde, Hasta = hasta, AsesorId = asesor, CategoriaId = categoria, DepartamentoId = departamento,
            MunicipioId = municipio, Origen = origenFijo is null ? origen : null, Estado = estado, Resultado = resultado,
            SoloAdministracion = soloAdmin, ResponsableAsesorId = responsableId, Canal = canal, PlanId = plan, Medio = medio,
            Pagina = pagina, Tamano = tamano
        }, errores);
    }

    private static string? Texto(IQueryCollection q, string clave)
    {
        var valor = q[clave].ToString();
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }

    private static string? Codigo(IQueryCollection q, string clave) => Texto(q, clave)?.ToUpperInvariant();

    private static DateOnly? LeerFecha(IQueryCollection q, string clave, Dictionary<string, string[]> errores)
    {
        var valor = Texto(q, clave);
        if (valor is null) return null;
        if (DateOnly.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)) return f;
        errores[clave] = [MensajesP8.Fecha];
        return null;
    }

    private static long? LeerEntero(IQueryCollection q, string clave, string mensaje, Dictionary<string, string[]> errores,
        long maximo = long.MaxValue)
    {
        var valor = Texto(q, clave);
        if (valor is null) return null;
        if (long.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1 && n <= maximo) return n;
        errores[clave] = [mensaje];
        return null;
    }
}

/// <summary>Página de una lista ya calculada en memoria.</summary>
public static class Paginas
{
    public static Contracts.PaginaFilas<T> De<T>(IReadOnlyList<T> todas, FiltrosReporte f)
    {
        var saltar = (long)(f.Pagina - 1) * f.Tamano;
        T[] items = saltar >= todas.Count ? [] : todas.Skip((int)saltar).Take(f.Tamano).ToArray();
        return new Contracts.PaginaFilas<T>(items, todas.Count, f.Pagina, f.Tamano);
    }
}
