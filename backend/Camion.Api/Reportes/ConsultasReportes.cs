using Camion.Api.Catalogo;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Leads;
using Camion.Api.Models;
using Camion.Api.Publicaciones;
using Camion.Api.Suscripciones;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Reportes;

/// <summary>Un reporte ya calculado: la respuesta JSON (con la página pedida), el total de filas y su CSV (D-71).</summary>
public interface IReporte
{
    object Respuesta { get; }
    int TotalFilas { get; }
    void EscribirCsv(Csv csv);
}

// P8 (HU-076 a HU-078, HU-080, D-68 A): cálculo de los cinco reportes del administrador. Solo lectura. Las cifras se
// agregan en memoria a partir de proyecciones pequeñas (sin nombres, teléfonos, correos ni mensajes de clientes, RN-SE-004);
// así las sumas de pesos no dependen del proveedor de base de datos y el CSV usa exactamente las mismas filas.
public static class ConsultasReportes
{
    public const string TipoVehiculos = "vehiculos";
    public const string TipoSolicitudes = "solicitudes";
    public const string TipoCreditos = "creditos";
    public const string TipoVentas = "ventas";
    public const string TipoSuscripciones = "suscripciones";

    private static readonly string[] EstadosPublicacionVivos = [Publicacion.Borrador, Publicacion.Publicada, Publicacion.Oculta];

    // ================================================================ comunes

    internal static async Task<Dictionary<long, string>> NombresAsesoresAsync(CamionDbContext db, CancellationToken ct) =>
        await (from a in db.PerfilesAsesor.AsNoTracking()
               join u in db.Users.AsNoTracking() on a.UsuarioId equals u.Id
               select new { a.Id, u.NombreCompleto }).ToDictionaryAsync(x => x.Id, x => x.NombreCompleto, ct);

    private static void Encabezado(Csv csv, string titulo, FiltrosReporte f)
    {
        csv.Fila("Reporte", titulo);
        csv.Fila("Desde", Etiquetas.Fecha(f.Desde));
        csv.Fila("Hasta", Etiquetas.Fecha(f.Hasta));
        foreach (var (clave, valor) in f.ParaAuditoria().Where(x => x.Key is not "desde" and not "hasta"))
            csv.Fila("Filtro " + clave, Convert.ToString(valor, System.Globalization.CultureInfo.InvariantCulture));
        csv.Vacia();
    }

    private static string Titulo(string? marca, string? linea, short? anio, string tipo) =>
        ReglasP4b.TituloVehiculo(marca, linea, anio, tipo);

    // ================================================================ 1. vehículos y publicaciones (HU-076)

    private sealed class VehInfo
    {
        public long Id { get; init; }
        public string Estado { get; init; } = "";
        public long TipoId { get; init; }
        public string Tipo { get; init; } = "";
        public string? Marca { get; init; }
        public string? Linea { get; init; }
        public short? Anio { get; init; }
        public long AsesorId { get; init; }
        public string Municipio { get; init; } = "";
        public string Departamento { get; init; } = "";
        public DateTime CreadoEn { get; init; }
    }

    private static IQueryable<Vehiculo> VehiculosFiltrados(CamionDbContext db, FiltrosReporte f)
    {
        var q = db.Vehiculos.AsNoTracking();
        if (f.AsesorId is { } a) q = q.Where(v => v.AsesorId == a);
        if (f.CategoriaId is { } c) q = q.Where(v => v.TipoVehiculoId == c);
        if (f.MunicipioId is { } m) q = q.Where(v => v.MunicipioId == m);
        else if (f.DepartamentoId is { } d) q = q.Where(v => v.Municipio!.DepartamentoId == d);
        return q;
    }

    public static async Task<IReporte> VehiculosAsync(CamionDbContext db, FiltrosReporte f, DateTime ahora, CancellationToken ct)
    {
        var vq = VehiculosFiltrados(db, f);
        var vehiculos = await vq.Select(v => new VehInfo
        {
            Id = v.Id, Estado = v.Estado, TipoId = v.TipoVehiculoId, Tipo = v.TipoVehiculo!.Nombre,
            Marca = v.Marca == null ? null : v.Marca.Nombre, Linea = v.Linea == null ? null : v.Linea.Nombre,
            Anio = v.ModeloAnio, AsesorId = v.AsesorId, Municipio = v.Municipio!.Nombre,
            Departamento = v.Municipio.Departamento!.Nombre, CreadoEn = v.CreadoEn
        }).ToListAsync(ct);

        var porEstado = Vehiculo.Estados.ToDictionary(e => e, e => vehiculos.Count(v => v.Estado == e));
        var tipos = await db.TiposVehiculo.AsNoTracking().Select(t => new { t.Id, t.Nombre, t.Activo }).ToListAsync(ct);
        var porCategoria = tipos
            .Where(t => f.CategoriaId is { } c ? t.Id == c : t.Activo || vehiculos.Any(v => v.TipoId == t.Id))
            .Select(t => new ConteoNombre(t.Id, t.Nombre, vehiculos.Count(v => v.TipoId == t.Id)))
            .OrderBy(t => t.Nombre, StringComparer.Ordinal).ToArray();

        var pubs = await db.Publicaciones.AsNoTracking().Where(p => vq.Any(v => v.Id == p.VehiculoId))
            .Select(p => new { p.Id, p.VehiculoId, p.EstadoPublicacion, p.EstadoComercial }).ToListAsync(ct);
        var porEstadoPub = EstadosPublicacionVivos
            .Concat(pubs.Select(p => p.EstadoPublicacion).Where(e => e != Publicacion.Eliminada && !EstadosPublicacionVivos.Contains(e)).Distinct())
            .ToDictionary(e => e, e => pubs.Count(p => p.EstadoPublicacion == e));
        var eliminadas = pubs.Count(p => p.EstadoPublicacion == Publicacion.Eliminada);
        var visibles = await ConsultasCatalogo.Visibles(db, ahora).CountAsync(p => vq.Any(v => v.Id == p.VehiculoId), ct);
        var destacadas = await ConsultasCatalogo.VisiblesConDestacado(db, ahora)
            .CountAsync(x => x.Destacado && vq.Any(v => v.Id == x.Publicacion.VehiculoId), ct);

        var pubAVehiculo = pubs.ToDictionary(p => p.Id, p => p.VehiculoId);
        var (ini, fin) = (f.InicioUtc, f.FinUtc);
        var contactos = (await db.Leads.AsNoTracking()
                .Where(l => l.Origen == Lead.OrigenContactoVehiculo && l.PublicacionId != null && l.CreadoEn >= ini && l.CreadoEn < fin)
                .Select(l => l.PublicacionId!.Value).ToListAsync(ct))
            .Where(pubAVehiculo.ContainsKey).GroupBy(p => pubAVehiculo[p])
            .ToDictionary(g => g.Key, g => g.Count());

        var porId = vehiculos.ToDictionary(v => v.Id);
        var masContactados = contactos.OrderByDescending(x => x.Value).ThenBy(x => x.Key).Take(10)
            .Select(x => new MasContactado(x.Key, Titulo(porId[x.Key].Marca, porId[x.Key].Linea, porId[x.Key].Anio, porId[x.Key].Tipo), x.Value))
            .ToArray();

        var asesores = await NombresAsesoresAsync(db, ct);
        var pubDe = pubs.GroupBy(p => p.VehiculoId).ToDictionary(g => g.Key, g => g
            .OrderBy(p => p.EstadoPublicacion == Publicacion.Eliminada ? 1 : 0).ThenByDescending(p => p.Id).First());
        var filas = vehiculos.Where(v => v.CreadoEn >= ini && v.CreadoEn < fin)
            .OrderByDescending(v => v.CreadoEn).ThenByDescending(v => v.Id)
            .Select(v => new FilaVehiculo(v.Id, Titulo(v.Marca, v.Linea, v.Anio, v.Tipo), v.Tipo, v.Estado,
                new Referencia(v.AsesorId, asesores.GetValueOrDefault(v.AsesorId) ?? ""), v.Municipio, v.Departamento,
                ConsultasCatalogo.Utc(v.CreadoEn),
                pubDe.TryGetValue(v.Id, out var p) ? new PublicacionFila(p.Id, ReglasP4b.Estado(p.EstadoPublicacion, p.EstadoComercial)) : null,
                contactos.GetValueOrDefault(v.Id)))
            .ToArray();

        var r = new ReporteVehiculosResponse(new Periodo(f.Desde, f.Hasta), porEstado, porCategoria, filas.Length, porEstadoPub,
            eliminadas, visibles, destacadas, masContactados, Paginas.De(filas, f));
        return new Reporte(r, filas.Length, csv =>
        {
            Encabezado(csv, "Vehículos y publicaciones", f);
            csv.Fila("Inventario por estado", "Total");
            foreach (var (e, n) in porEstado) csv.Fila(Etiquetas.De(Etiquetas.EstadoVehiculo, e), Csv.Num(n));
            csv.Vacia().Fila("Inventario por categoría", "Total");
            foreach (var c in porCategoria) csv.Fila(c.Nombre, Csv.Num(c.Total));
            csv.Vacia().Fila("Concepto", "Valor");
            csv.Fila("Registrados en el período", Csv.Num(filas.Length));
            foreach (var (e, n) in porEstadoPub) csv.Fila("Publicaciones " + Etiquetas.De(Etiquetas.EstadoPublicacion, e), Csv.Num(n));
            csv.Fila("Publicaciones eliminadas", Csv.Num(eliminadas));
            csv.Fila("Publicaciones visibles hoy", Csv.Num(visibles));
            csv.Fila("Publicaciones destacadas hoy", Csv.Num(destacadas));
            csv.Vacia().Fila("Más contactados: vehículo", "Título", "Contactos");
            foreach (var m in masContactados) csv.Fila(Csv.Num(m.VehiculoId), m.Titulo, Csv.Num(m.Contactos));
            csv.Vacia().Fila("Filas");
            csv.Fila("Vehículo", "Título", "Categoría", "Estado", "Asesor", "Municipio", "Departamento", "Registrado",
                "Publicación", "Estado publicación", "Contactos en el período");
            foreach (var x in filas)
                csv.Fila(Csv.Num(x.VehiculoId), x.Titulo, x.Categoria, Etiquetas.De(Etiquetas.EstadoVehiculo, x.Estado), x.Asesor.Nombre,
                    x.Municipio, x.Departamento, Etiquetas.FechaUtc(x.RegistradoEn),
                    x.Publicacion is null ? "" : Csv.Num(x.Publicacion.Id),
                    Etiquetas.De(Etiquetas.EstadoPublicacion, x.Publicacion?.Estado), Csv.Num(x.ContactosEnPeriodo));
        });
    }

    // ================================================================ 2. solicitudes (HU-077) y 3. créditos (HU-078)

    private sealed class LeadInfo
    {
        public long Id { get; init; }
        public string Origen { get; init; } = "";
        public string Estado { get; init; } = "";
        public string? Resultado { get; init; }
        public DateTime CreadoEn { get; init; }
        public DateTime? CerradoEn { get; init; }
        public long? Responsable { get; init; }
        public long? PublicacionId { get; init; }
        public long? VehiculoId { get; init; }
        public string? Tipo { get; init; }
        public string? Marca { get; init; }
        public string? Linea { get; init; }
        public short? Anio { get; init; }
        public bool ConNotas { get; init; }

        public VehiculoRef? Vehiculo() =>
            VehiculoId is { } id ? new VehiculoRef(id, Titulo(Marca, Linea, Anio, Tipo ?? "")) : null;
    }

    private static IQueryable<LeadInfo> ProyectarLeads(CamionDbContext db, IQueryable<Lead> q) =>
        q.Select(l => new LeadInfo
        {
            Id = l.Id, Origen = l.Origen, Estado = l.Estado, Resultado = l.ResultadoCierre, CreadoEn = l.CreadoEn,
            CerradoEn = l.CerradoEn, Responsable = l.ResponsableAsesorId, PublicacionId = l.PublicacionId,
            VehiculoId = l.Publicacion == null ? null : (long?)l.Publicacion.VehiculoId,
            Tipo = l.Publicacion == null ? null : l.Publicacion.Vehiculo!.TipoVehiculo!.Nombre,
            Marca = l.Publicacion == null || l.Publicacion.Vehiculo!.Marca == null ? null : l.Publicacion.Vehiculo.Marca.Nombre,
            Linea = l.Publicacion == null || l.Publicacion.Vehiculo!.Linea == null ? null : l.Publicacion.Vehiculo.Linea.Nombre,
            Anio = l.Publicacion == null ? null : (short?)l.Publicacion.Vehiculo!.ModeloAnio,
            ConNotas = db.EventosLead.Any(e => e.LeadId == l.Id && e.Tipo == EventoLead.Nota)
        });

    private static bool CreadaEn(LeadInfo l, FiltrosReporte f) => l.CreadoEn >= f.InicioUtc && l.CreadoEn < f.FinUtc;

    private static bool CerradaEn(LeadInfo l, FiltrosReporte f) =>
        l.Estado == Lead.Cerrado && l.CerradoEn is { } c && c >= f.InicioUtc && c < f.FinUtc;

    /// <summary>Leads creados o cerrados en el período (los dos conjuntos que usan los reportes), con los filtros en SQL.</summary>
    private static Task<List<LeadInfo>> LeadsDelPeriodoAsync(CamionDbContext db, IQueryable<Lead> q, FiltrosReporte f,
        CancellationToken ct)
    {
        var (ini, fin) = (f.InicioUtc, f.FinUtc);
        return ProyectarLeads(db, q.Where(l => (l.CreadoEn >= ini && l.CreadoEn < fin) ||
                                               (l.Estado == Lead.Cerrado && l.CerradoEn >= ini && l.CerradoEn < fin)))
            .ToListAsync(ct);
    }

    /// <summary>CA-P8-21: media en días (1 decimal) entre la creación y el primer cierre; null si no hay cerradas.</summary>
    internal static async Task<Dictionary<long, DateTime>> PrimerCierreAsync(CamionDbContext db, IReadOnlyCollection<long> ids,
        CancellationToken ct)
    {
        var resultado = new Dictionary<long, DateTime>();
        foreach (var lote in ids.Chunk(500))
        {
            var eventos = await db.EventosLead.AsNoTracking()
                .Where(e => e.Tipo == EventoLead.CerradoTipo && lote.Contains(e.LeadId))
                .Select(e => new { e.LeadId, e.CreadoEn }).ToListAsync(ct);
            foreach (var e in eventos)
                if (!resultado.TryGetValue(e.LeadId, out var actual) || e.CreadoEn < actual) resultado[e.LeadId] = e.CreadoEn;
        }
        return resultado;
    }

    internal static double? MediaDias(IEnumerable<(DateTime Creado, DateTime Cerrado)> pares)
    {
        var dias = pares.Select(p => Math.Max(0, (p.Cerrado - p.Creado).TotalDays)).ToList();
        return dias.Count == 0 ? null : Math.Round(dias.Average(), 1, MidpointRounding.AwayFromZero);
    }

    public static async Task<IReporte> SolicitudesAsync(CamionDbContext db, FiltrosReporte f, CancellationToken ct)
    {
        var q = db.Leads.AsNoTracking();
        if (f.Origen is { } o) q = q.Where(l => l.Origen == o);
        if (f.Estado is { } e) q = q.Where(l => l.Estado == e);
        if (f.Resultado is { } r) q = q.Where(l => l.ResultadoCierre == r);
        if (f.SoloAdministracion) q = q.Where(l => l.ResponsableAsesorId == null);
        if (f.ResponsableAsesorId is { } ra) q = q.Where(l => l.ResponsableAsesorId == ra);
        if (f.CategoriaId is { } c) q = q.Where(l => l.Publicacion != null && l.Publicacion.Vehiculo!.TipoVehiculoId == c);
        var leads = await LeadsDelPeriodoAsync(db, q, f, ct);

        var creadas = leads.Where(l => CreadaEn(l, f)).ToList();
        var cerradas = leads.Where(l => CerradaEn(l, f)).ToList();
        var porOrigen = Lead.Origenes.Select(org =>
        {
            var g = creadas.Where(l => l.Origen == org).ToList();
            return new SolicitudesOrigen(org, g.Count, g.Count(l => l.Estado == Lead.Abierto), g.Count(l => l.Estado == Lead.Cerrado));
        }).ToArray();
        var origenes = f.Origen is null ? Lead.Origenes : [f.Origen];
        var porResultado = origenes.SelectMany(org => Lead.ResultadosDe(org)
                .Where(res => f.Resultado is null || res == f.Resultado)
                .Select(res => new CerradasResultado(org, res, cerradas.Count(l => l.Origen == org && l.Resultado == res))))
            .ToArray();
        var primer = await PrimerCierreAsync(db, cerradas.Select(l => l.Id).ToList(), ct);
        var tiempos = Lead.Origenes.Select(org =>
        {
            var g = cerradas.Where(l => l.Origen == org).ToList();
            return new TiempoCierre(org, g.Count,
                MediaDias(g.Select(l => (l.CreadoEn, primer.TryGetValue(l.Id, out var p) ? p : l.CerradoEn!.Value))));
        }).ToArray();

        // CA-P8-19: contactos de vehículo creados en el período, por responsable actual y por reasignaciones.
        var asesores = await NombresAsesoresAsync(db, ct);
        var contactos = creadas.Where(l => l.Origen == Lead.OrigenContactoVehiculo).ToList();
        var idsContactos = contactos.Select(l => l.Id).ToList();
        var reasignaciones = new List<(long LeadId, bool Automatico, long? Nuevo)>();
        foreach (var lote in idsContactos.Chunk(500))
            reasignaciones.AddRange((await db.EventosLead.AsNoTracking()
                    .Where(ev => ev.Tipo == EventoLead.Reasignado && lote.Contains(ev.LeadId))
                    .Select(ev => new { ev.LeadId, ev.Automatico, ev.ResponsableNuevoAsesorId }).ToListAsync(ct))
                .Select(x => (x.LeadId, x.Automatico, x.ResponsableNuevoAsesorId)));
        var porResponsable = new ContactosPorResponsable(
            contactos.Count(l => l.Responsable is null),
            contactos.Where(l => l.Responsable is not null).GroupBy(l => l.Responsable!.Value)
                .Select(g => new ConteoNombre(g.Key, asesores.GetValueOrDefault(g.Key) ?? "", g.Count()))
                .OrderByDescending(x => x.Total).ThenBy(x => x.Nombre, StringComparer.Ordinal).ToArray(),
            reasignaciones.Where(x => !x.Automatico).Select(x => x.LeadId).Distinct().Count(),
            reasignaciones.Where(x => x.Automatico && x.Nuevo is null).Select(x => x.LeadId).Distinct().Count());

        var filas = creadas.OrderByDescending(l => l.CreadoEn).ThenByDescending(l => l.Id)
            .Select(l => new FilaSolicitud(l.Id, ConsultasCatalogo.Utc(l.CreadoEn), l.Origen, l.Estado, l.Resultado,
                l.CerradoEn is { } ce ? ConsultasCatalogo.Utc(ce) : null, Responsable(l.Responsable, asesores), l.Vehiculo()))
            .ToArray();

        var resp = new ReporteSolicitudesResponse(new Periodo(f.Desde, f.Hasta), creadas.Count, porOrigen, cerradas.Count,
            porResultado, tiempos, porResponsable, Paginas.De(filas, f));
        return new Reporte(resp, filas.Length, csv =>
        {
            Encabezado(csv, "Solicitudes", f);
            csv.Fila("Origen", "Recibidas", "Abiertas", "Cerradas");
            foreach (var x in porOrigen)
                csv.Fila(Etiquetas.De(Etiquetas.OrigenLead, x.Origen), Csv.Num(x.Recibidas), Csv.Num(x.Abiertas), Csv.Num(x.Cerradas));
            csv.Fila("Total recibidas", Csv.Num(creadas.Count));
            csv.Vacia().Fila("Cerradas en el período: origen", "Resultado", "Total");
            foreach (var x in porResultado)
                csv.Fila(Etiquetas.De(Etiquetas.OrigenLead, x.Origen), Etiquetas.De(Etiquetas.Resultado, x.Resultado), Csv.Num(x.Total));
            csv.Vacia().Fila("Tiempo medio hasta el cierre: origen", "Cerradas", "Días");
            foreach (var x in tiempos)
                csv.Fila(Etiquetas.De(Etiquetas.OrigenLead, x.Origen), Csv.Num(x.Cerradas), Etiquetas.Porcentaje(x.Dias));
            csv.Vacia().Fila("Contactos de vehículo por responsable", "Total");
            csv.Fila("Administración", Csv.Num(porResponsable.Administracion));
            foreach (var a in porResponsable.Asesores) csv.Fila(a.Nombre, Csv.Num(a.Total));
            csv.Fila("Reasignados a mano", Csv.Num(porResponsable.ReasignadosAMano));
            csv.Fila("Pasados solos a Administración", Csv.Num(porResponsable.PasadosAAdministracion));
            csv.Vacia().Fila("Filas");
            csv.Fila("Solicitud", "Creada", "Origen", "Estado", "Resultado", "Cerrada", "Responsable", "Vehículo", "Título");
            foreach (var x in filas)
                csv.Fila(Csv.Num(x.Id), Etiquetas.FechaUtc(x.CreadoEn), Etiquetas.De(Etiquetas.OrigenLead, x.Origen),
                    Etiquetas.De(Etiquetas.EstadoLead, x.Estado), Etiquetas.De(Etiquetas.Resultado, x.Resultado),
                    x.CerradoEn is { } c2 ? Etiquetas.FechaUtc(c2) : "", x.Responsable.Nombre,
                    x.Vehiculo is null ? "" : Csv.Num(x.Vehiculo.Id), x.Vehiculo?.Titulo);
        });
    }

    private static ResponsableFila Responsable(long? asesorId, Dictionary<long, string> asesores) =>
        asesorId is { } a
            ? new ResponsableFila("ASESOR", a, asesores.GetValueOrDefault(a) ?? "")
            : new ResponsableFila(ReglasLeads.ResponsableAdministracion, null, "Administración");

    public static async Task<IReporte> CreditosAsync(CamionDbContext db, FiltrosReporte f, CancellationToken ct)
    {
        var q = db.Leads.AsNoTracking().Where(l => l.Origen == Lead.OrigenCredito);
        if (f.Resultado is { } r) q = q.Where(l => l.ResultadoCierre == r);
        if (f.Canal == FiltrosReporte.CanalPortada) q = q.Where(l => l.PublicacionId == null);
        if (f.Canal == FiltrosReporte.CanalFicha) q = q.Where(l => l.PublicacionId != null);
        var leads = (await LeadsDelPeriodoAsync(db, q, f, ct))
            .Where(l => f.Estado is null || EstadoCredito(l) == f.Estado).ToList();

        var creados = leads.Where(l => CreadaEn(l, f)).ToList();
        var cerrados = leads.Where(l => CerradaEn(l, f)).ToList();
        var porEstado = FiltrosReporte.EstadosCredito.ToDictionary(e => e, e => creados.Count(l => EstadoCredito(l) == e));
        var porCanal = FiltrosReporte.Canales.ToDictionary(c => c, c => creados.Count(l => Canal(l) == c));
        var porResultado = Lead.ResultadosCredito.Select(res => new ConteoResultado(res, cerrados.Count(l => l.Resultado == res)))
            .ToArray();
        double? porcentaje = cerrados.Count == 0
            ? null
            : Math.Round(100.0 * cerrados.Count(l => l.Resultado == Lead.RemitidoEntidad) / cerrados.Count, 1, MidpointRounding.AwayFromZero);
        var filas = creados.OrderByDescending(l => l.CreadoEn).ThenByDescending(l => l.Id)
            .Select(l => new FilaCredito(l.Id, ConsultasCatalogo.Utc(l.CreadoEn), Canal(l), EstadoCredito(l), l.Resultado,
                l.CerradoEn is { } ce ? ConsultasCatalogo.Utc(ce) : null, l.Vehiculo()))
            .ToArray();

        var resp = new ReporteCreditosResponse(new Periodo(f.Desde, f.Hasta), creados.Count, porEstado, porCanal, cerrados.Count,
            porResultado, porcentaje, Paginas.De(filas, f));
        return new Reporte(resp, filas.Length, csv =>
        {
            Encabezado(csv, "Créditos", f);
            csv.Fila("Concepto", "Valor");
            csv.Fila("Recibidos", Csv.Num(creados.Count));
            foreach (var (e, n) in porEstado) csv.Fila("Estado " + Etiquetas.De(Etiquetas.EstadoLead, e), Csv.Num(n));
            foreach (var (c, n) in porCanal) csv.Fila("Pedidos desde " + Etiquetas.De(Etiquetas.Otras, c), Csv.Num(n));
            csv.Fila("Cerrados en el período", Csv.Num(cerrados.Count));
            csv.Fila("Porcentaje remitido", Etiquetas.Porcentaje(porcentaje));
            csv.Vacia().Fila("Resultado", "Total");
            foreach (var x in porResultado) csv.Fila(Etiquetas.De(Etiquetas.Resultado, x.Resultado), Csv.Num(x.Total));
            csv.Vacia().Fila("Filas");
            csv.Fila("Crédito", "Creado", "Canal", "Estado", "Resultado", "Cerrado", "Vehículo", "Título");
            foreach (var x in filas)
                csv.Fila(Csv.Num(x.Id), Etiquetas.FechaUtc(x.CreadoEn), Etiquetas.De(Etiquetas.Otras, x.Canal),
                    Etiquetas.De(Etiquetas.EstadoLead, x.Estado), Etiquetas.De(Etiquetas.Resultado, x.Resultado),
                    x.CerradoEn is { } c2 ? Etiquetas.FechaUtc(c2) : "", x.Vehiculo is null ? "" : Csv.Num(x.Vehiculo.Id),
                    x.Vehiculo?.Titulo);
        });
    }

    private static string EstadoCredito(LeadInfo l) => ReglasLeads.EstadoGestion(l.Origen, l.Estado, l.ConNotas)!;

    private static string Canal(LeadInfo l) => l.PublicacionId is null ? FiltrosReporte.CanalPortada : FiltrosReporte.CanalFicha;

    // ================================================================ 4. ventas (HU-080, D-39)

    internal sealed class VentaInfo
    {
        public long Id { get; init; }
        public DateOnly FechaVenta { get; init; }
        public decimal Precio { get; init; }
        public string Origen { get; init; } = "";
        public bool Revertida { get; init; }
        public long VehiculoId { get; init; }
        public long TipoId { get; init; }
        public string Tipo { get; init; } = "";
        public string? Marca { get; init; }
        public string? Linea { get; init; }
        public short? Anio { get; init; }
        public long AsesorId { get; init; }
        public long Valor => ConsultasCatalogo.Pesos(Precio);
    }

    /// <summary>Ventas con fecha de venta en [desde, hasta] y el vehículo de hoy (mismo alcance que GET /api/ventas).</summary>
    internal static Task<List<VentaInfo>> VentasAsync(CamionDbContext db, DateOnly desde, DateOnly hasta, long? asesorId,
        long? categoriaId, string? origen, CancellationToken ct)
    {
        var q = from venta in db.VentasVehiculo.AsNoTracking()
                join v in db.Vehiculos.AsNoTracking() on venta.VehiculoId equals v.Id
                where venta.FechaVenta >= desde && venta.FechaVenta <= hasta
                select new { venta, v };
        if (asesorId is { } a) q = q.Where(x => x.v.AsesorId == a);
        if (categoriaId is { } c) q = q.Where(x => x.v.TipoVehiculoId == c);
        if (origen is { } o) q = q.Where(x => x.venta.Origen == o);
        return q.Select(x => new VentaInfo
        {
            Id = x.venta.Id, FechaVenta = x.venta.FechaVenta, Precio = x.venta.PrecioFinalCop, Origen = x.venta.Origen,
            Revertida = x.venta.RevertidaEn != null, VehiculoId = x.v.Id, TipoId = x.v.TipoVehiculoId,
            Tipo = x.v.TipoVehiculo!.Nombre, Marca = x.v.Marca == null ? null : x.v.Marca.Nombre,
            Linea = x.v.Linea == null ? null : x.v.Linea.Nombre, Anio = x.v.ModeloAnio, AsesorId = x.v.AsesorId
        }).ToListAsync(ct);
    }

    internal static VentasOrigen[] PorOrigen(IReadOnlyCollection<VentaInfo> vigentes) =>
        VentaVehiculo.Origenes.Select(o =>
        {
            var g = vigentes.Where(v => v.Origen == o).ToList();
            return new VentasOrigen(o, g.Count, g.Sum(v => v.Valor));
        }).ToArray();

    public static async Task<IReporte> VentasReporteAsync(CamionDbContext db, FiltrosReporte f, CancellationToken ct)
    {
        var ventas = await VentasAsync(db, f.Desde, f.Hasta, f.AsesorId, f.CategoriaId, f.Origen, ct);
        var vigentes = ventas.Where(v => !v.Revertida).ToList();
        var asesores = await NombresAsesoresAsync(db, ct);
        var porOrigen = PorOrigen(vigentes);
        var porCategoria = vigentes.GroupBy(v => (v.TipoId, v.Tipo))
            .Select(g => new ConteoValorNombre(g.Key.TipoId, g.Key.Tipo, g.Count(), g.Sum(v => v.Valor)))
            .OrderBy(x => x.Nombre, StringComparer.Ordinal).ToArray();
        var porAsesor = vigentes.GroupBy(v => v.AsesorId)
            .Select(g => new ConteoValorNombre(g.Key, asesores.GetValueOrDefault(g.Key) ?? "", g.Count(), g.Sum(v => v.Valor)))
            .OrderByDescending(x => x.ValorCop).ThenBy(x => x.Nombre, StringComparer.Ordinal).ToArray();
        var filas = ventas.OrderByDescending(v => v.FechaVenta).ThenByDescending(v => v.Id)
            .Select(v => new FilaVenta(v.Id, v.FechaVenta, new VehiculoRef(v.VehiculoId, Titulo(v.Marca, v.Linea, v.Anio, v.Tipo)),
                v.Tipo, new Referencia(v.AsesorId, asesores.GetValueOrDefault(v.AsesorId) ?? ""), v.Origen, v.Valor,
                v.Revertida ? "REVERTIDA" : "VIGENTE"))
            .ToArray();
        var totales = new CantidadValor(vigentes.Count, vigentes.Sum(v => v.Valor));
        var revertidas = ventas.Count(v => v.Revertida);

        var resp = new ReporteVentasResponse(new Periodo(f.Desde, f.Hasta), totales, revertidas, porOrigen, porCategoria, porAsesor,
            Paginas.De(filas, f));
        return new Reporte(resp, filas.Length, csv =>
        {
            Encabezado(csv, "Ventas", f);
            csv.Fila("Concepto", "Cantidad", "Valor (COP)");
            csv.Fila("Ventas vigentes", Csv.Num(totales.Cantidad), Csv.Num(totales.ValorCop));
            csv.Fila("Revertidas (no suman)", Csv.Num(revertidas), "");
            csv.Vacia().Fila("Origen", "Cantidad", "Valor (COP)");
            foreach (var x in porOrigen) csv.Fila(Etiquetas.De(Etiquetas.OrigenVenta, x.Origen), Csv.Num(x.Cantidad), Csv.Num(x.ValorCop));
            csv.Vacia().Fila("Categoría", "Cantidad", "Valor (COP)");
            foreach (var x in porCategoria) csv.Fila(x.Nombre, Csv.Num(x.Cantidad), Csv.Num(x.ValorCop));
            csv.Vacia().Fila("Asesor", "Cantidad", "Valor (COP)");
            foreach (var x in porAsesor) csv.Fila(x.Nombre, Csv.Num(x.Cantidad), Csv.Num(x.ValorCop));
            csv.Vacia().Fila("Filas");
            csv.Fila("Venta", "Fecha de venta", "Vehículo", "Título", "Categoría", "Asesor", "Origen", "Precio final (COP)", "Estado");
            foreach (var x in filas)
                csv.Fila(Csv.Num(x.Id), Etiquetas.Fecha(x.FechaVenta), Csv.Num(x.Vehiculo.Id), x.Vehiculo.Titulo, x.Categoria,
                    x.Asesor.Nombre, Etiquetas.De(Etiquetas.OrigenVenta, x.Origen), Csv.Num(x.PrecioFinalCop),
                    Etiquetas.De(Etiquetas.Otras, x.Estado));
        });
    }

    // ================================================================ 5. suscripciones y pagos (D-61 B, D-66, D-67)

    internal sealed class PagoInfo
    {
        public long Id { get; init; }
        public long SuscripcionId { get; init; }
        public decimal ValorCop { get; init; }
        public DateOnly? FechaPago { get; init; }
        public DateTime? RegistradoEn { get; init; }
        public DateTime CreadoEn { get; init; }
        public string? Medio { get; init; }
        public string? Concepto { get; init; }
        public string Estado { get; init; } = "";
        public long Valor => ConsultasCatalogo.Pesos(ValorCop);
        public DateOnly Fecha => FechaPago ?? FiltrosReporte.DiaColombia(RegistradoEn ?? CreadoEn);
    }

    /// <summary>CA-P8-31, CA-P8-32: pagos del período por fecha de pago o, si no tiene, por fecha de registro.</summary>
    internal static Task<List<PagoInfo>> PagosAsync(CamionDbContext db, IQueryable<Pago> q, FiltrosReporte f, CancellationToken ct)
    {
        var (desde, hasta, ini, fin) = (f.Desde, f.Hasta, f.InicioUtc, f.FinUtc);
        return q.Where(p => (p.FechaPago != null && p.FechaPago >= desde && p.FechaPago <= hasta) ||
                            (p.FechaPago == null && (p.RegistradoEn ?? p.CreadoEn) >= ini && (p.RegistradoEn ?? p.CreadoEn) < fin))
            .Select(p => new PagoInfo
            {
                Id = p.Id, SuscripcionId = p.SuscripcionId, ValorCop = p.ValorCop, FechaPago = p.FechaPago,
                RegistradoEn = p.RegistradoEn, CreadoEn = p.CreadoEn, Medio = p.Medio, Concepto = p.Concepto, Estado = p.Estado
            }).ToListAsync(ct);
    }

    public static async Task<IReporte> SuscripcionesAsync(CamionDbContext db, FiltrosReporte f, DateTime ahora, CancellationToken ct)
    {
        var sq = db.Suscripciones.AsNoTracking();
        if (f.AsesorId is { } a) sq = sq.Where(s => s.AsesorId == a);
        if (f.PlanId is { } p) sq = sq.Where(s => s.PlanId == p);
        var subs = await sq.ToListAsync(ct);
        var planes = await db.Planes.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);
        var asesores = await NombresAsesoresAsync(db, ct);
        var porId = subs.ToDictionary(s => s.Id);

        var porEstado = Suscripcion.Estados.ToDictionary(e => e, e => subs.Count(s => ReglasP4p.EstadoEfectivo(s, ahora) == e));
        var vigentesPorPlan = subs.Where(s => (s.Estado == Suscripcion.Activa || s.Estado == Suscripcion.Reactivada) &&
                                              s.InicioVigencia <= ahora && s.FinVigencia > ahora)
            .GroupBy(s => s.PlanId).Select(g => new ConteoNombre(g.Key, planes.GetValueOrDefault(g.Key) ?? "", g.Count()))
            .OrderBy(x => x.Nombre, StringComparer.Ordinal).ToArray();

        var (ini, fin) = (f.InicioUtc, f.FinUtc);
        var acciones = new[] { Auth.Auditoria.Asignar, Auth.Auditoria.Renovar, Auth.Auditoria.CambiarPlan };
        var eventos = (await db.AuditoriaEventos.AsNoTracking()
                .Where(ev => ev.Entidad == Auth.Auditoria.EntidadSuscripcion && acciones.Contains(ev.Accion) &&
                             ev.CreadoEn >= ini && ev.CreadoEn < fin)
                .Select(ev => new { ev.EntidadId, ev.Accion, ConAntes = ev.DatosAnteriores != null }).ToListAsync(ct))
            .Where(ev => long.TryParse(ev.EntidadId, out var id) && porId.ContainsKey(id)).ToList();
        var asignadas = eventos.Count(ev => ev.Accion == Auth.Auditoria.Asignar);
        var renovadas = eventos.Count(ev => ev.Accion == Auth.Auditoria.Renovar);
        // El cambio de plan deja dos eventos (suscripción anterior y nueva); se cuenta el de la nueva (sin datos anteriores).
        var cambios = eventos.Count(ev => ev.Accion == Auth.Auditoria.CambiarPlan && !ev.ConAntes);
        var vencidas = subs.Count(s => s.FinVigencia is { } fv && fv >= ini && fv < fin && fv <= ahora &&
                                       (s.Estado == Suscripcion.Activa || s.Estado == Suscripcion.Reactivada ||
                                        s.Estado == Suscripcion.Vencida));

        var pq = db.Pagos.AsNoTracking().Where(x => sq.Any(s => s.Id == x.SuscripcionId));
        if (f.Medio is { } m) pq = pq.Where(x => x.Medio == m);
        var pagos = await PagosAsync(db, pq, f, ct);
        // Solo suman los REGISTRADO (igual que el total de "Pagos" de P7); los anulados van aparte y las filas anteriores a P7
        // (PENDIENTE, APROBADO, RECHAZADO del script 002) solo aparecen en las filas y en "sin fecha de pago".
        var validos = pagos.Where(x => x.Estado == Pago.Registrado).ToList();
        var anulados = pagos.Where(x => x.Estado == Pago.Anulado).ToList();
        var medios = Pago.Medios.Concat(validos.Any(x => x.Medio is null) ? new[] { Etiquetas.SinDato } : Array.Empty<string>()).ToArray();
        var conceptos = new[] { Pago.ConceptoAsignacion, Pago.ConceptoRenovacion, Pago.ConceptoCambioPlan, Pago.ConceptoPosterior }
            .Concat(validos.Any(x => x.Concepto is null) ? new[] { Etiquetas.SinDato } : Array.Empty<string>()).ToArray();
        long PlanDe(PagoInfo x) => porId.TryGetValue(x.SuscripcionId, out var s) ? s.PlanId : 0;
        long AsesorDe(PagoInfo x) => porId.TryGetValue(x.SuscripcionId, out var s) ? s.AsesorId : 0;
        var resumen = new ResumenPagos(
            validos.Count, validos.Sum(x => x.Valor),
            medios.Select(c => Grupo(c, validos.Where(x => (x.Medio ?? Etiquetas.SinDato) == c))).ToArray(),
            validos.GroupBy(PlanDe).Select(g => new ConteoValorNombre(g.Key, planes.GetValueOrDefault(g.Key) ?? "", g.Count(),
                g.Sum(x => x.Valor))).OrderBy(x => x.Nombre, StringComparer.Ordinal).ToArray(),
            conceptos.Select(c => Grupo(c, validos.Where(x => (x.Concepto ?? Etiquetas.SinDato) == c))).ToArray(),
            new CantidadValor(anulados.Count, anulados.Sum(x => x.Valor)),
            pagos.Count(x => x.FechaPago is null));
        var filas = pagos.OrderByDescending(x => x.Fecha).ThenByDescending(x => x.Id)
            .Select(x => new FilaPago(x.Id, x.Fecha, x.FechaPago is null, x.SuscripcionId,
                new Referencia(AsesorDe(x), asesores.GetValueOrDefault(AsesorDe(x)) ?? ""),
                new Referencia(PlanDe(x), planes.GetValueOrDefault(PlanDe(x)) ?? ""),
                x.Medio ?? Etiquetas.SinDato, x.Concepto ?? Etiquetas.SinDato, x.Valor, x.Estado))
            .ToArray();

        var resp = new ReporteSuscripcionesResponse(new Periodo(f.Desde, f.Hasta), porEstado, vigentesPorPlan, asignadas, renovadas,
            cambios, vencidas, resumen, MensajesP8.AvisoPagos, Paginas.De(filas, f));
        return new Reporte(resp, filas.Length, csv =>
        {
            Encabezado(csv, "Suscripciones y pagos", f);
            csv.Fila(MensajesP8.AvisoPagos).Vacia();
            csv.Fila("Suscripciones por estado (hoy)", "Total");
            foreach (var (e, n) in porEstado) csv.Fila(Etiquetas.De(Etiquetas.EstadoSuscripcion, e), Csv.Num(n));
            csv.Vacia().Fila("Vigentes por plan", "Total");
            foreach (var x in vigentesPorPlan) csv.Fila(x.Nombre, Csv.Num(x.Total));
            csv.Vacia().Fila("Concepto", "Valor");
            csv.Fila("Asignadas en el período", Csv.Num(asignadas));
            csv.Fila("Renovadas en el período", Csv.Num(renovadas));
            csv.Fila("Cambios de plan en el período", Csv.Num(cambios));
            csv.Fila("Vencidas en el período", Csv.Num(vencidas));
            csv.Fila("Pagos registrados", Csv.Num(resumen.Cantidad));
            csv.Fila("Total recibido (COP)", Csv.Num(resumen.TotalRecibidoCop));
            csv.Fila("Pagos anulados (no suman)", Csv.Num(resumen.Anulados.Cantidad));
            csv.Fila("Valor anulado (COP)", Csv.Num(resumen.Anulados.ValorCop));
            csv.Fila("Pagos sin fecha de pago", Csv.Num(resumen.SinFechaPago));
            csv.Vacia().Fila("Medio", "Cantidad", "Valor (COP)");
            foreach (var x in resumen.PorMedio) csv.Fila(Etiquetas.De(Etiquetas.Medio, x.Codigo), Csv.Num(x.Cantidad), Csv.Num(x.ValorCop));
            csv.Vacia().Fila("Plan", "Cantidad", "Valor (COP)");
            foreach (var x in resumen.PorPlan) csv.Fila(x.Nombre, Csv.Num(x.Cantidad), Csv.Num(x.ValorCop));
            csv.Vacia().Fila("Concepto del pago", "Cantidad", "Valor (COP)");
            foreach (var x in resumen.PorConcepto) csv.Fila(Etiquetas.De(Etiquetas.Concepto, x.Codigo), Csv.Num(x.Cantidad), Csv.Num(x.ValorCop));
            csv.Vacia().Fila("Filas");
            csv.Fila("Pago", "Fecha", "Sin fecha de pago", "Suscripción", "Asesor", "Plan", "Medio", "Concepto", "Valor (COP)", "Estado");
            foreach (var x in filas)
                csv.Fila(Csv.Num(x.Id), Etiquetas.Fecha(x.Fecha), x.SinFechaPago ? "Sí" : "No", Csv.Num(x.SuscripcionId), x.Asesor.Nombre,
                    x.Plan.Nombre, Etiquetas.De(Etiquetas.Medio, x.Medio), Etiquetas.De(Etiquetas.Concepto, x.Concepto),
                    Csv.Num(x.ValorCop), Etiquetas.De(Etiquetas.Otras, x.Estado));
        });
    }

    private static ConteoValorCodigo Grupo(string codigo, IEnumerable<PagoInfo> pagos)
    {
        var lista = pagos.ToList();
        return new ConteoValorCodigo(codigo, lista.Count, lista.Sum(x => x.Valor));
    }

    private sealed class Reporte(object respuesta, int total, Action<Csv> csv) : IReporte
    {
        public object Respuesta { get; } = respuesta;
        public int TotalFilas { get; } = total;
        public void EscribirCsv(Csv destino) => csv(destino);
    }
}
