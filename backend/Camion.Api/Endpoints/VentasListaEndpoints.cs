using System.Globalization;
using System.Security.Claims;
using Camion.Api.Auth;
using Camion.Api.Catalogo;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Models;
using Camion.Api.Publicaciones;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Endpoints;

// P7 (minuta §6 "conserva el historial", M11, HU-064, D-62 A): lista de ventas, vigentes y revertidas. El administrador
// ve todas; el asesor, las de los vehículos que hoy son suyos (mismo alcance que el inventario; lo ajeno da 404).
// Sin datos de compradores ni de leads (CA-P7-12) y sin línea de tiempo de auditoría (D-62 d A).
public static class VentasListaEndpoints
{
    public const string EstadoVigente = "VIGENTE";
    public const string EstadoRevertida = "REVERTIDA";
    private static readonly string[] Estados = [EstadoVigente, EstadoRevertida];

    public const string MensajeFecha = "Escribe una fecha válida (aaaa-mm-dd).";
    public const string MensajeRangoFechas = "La fecha inicial no puede ser posterior a la final.";
    public const string MensajeOrigen = "Origen no válido.";
    public const string MensajeEstado = "Estado no válido.";
    public const string MensajeAsesor = "Asesor no válido.";
    public const string MensajePagina = "Página o tamaño no válidos.";

    private const int TamanoPorDefecto = 20;
    private const int TamanoMaximo = 100;

    public static IEndpointRouteBuilder MapVentasListaEndpoints(this IEndpointRouteBuilder app)
    {
        // CA-P7-01 a CA-P7-05, CA-P7-14.
        app.MapGet("/api/ventas", async (string? desde, string? hasta, string? origen, string? estado, string? asesorId,
            string? pagina, string? tamano, ClaimsPrincipal principal, CamionDbContext db, CancellationToken ct) =>
        {
            var actor = await InventarioEndpoints.ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();

            var errores = new Dictionary<string, string[]>();
            var fechaDesde = LeerFecha(desde, "desde", errores);
            var fechaHasta = LeerFecha(hasta, "hasta", errores);
            if (fechaDesde is not null && fechaHasta is not null && fechaDesde > fechaHasta)
                errores["hasta"] = [MensajeRangoFechas];
            var origenFiltro = string.IsNullOrWhiteSpace(origen) ? null : origen.Trim().ToUpperInvariant();
            if (origenFiltro is not null && !VentaVehiculo.Origenes.Contains(origenFiltro)) errores["origen"] = [MensajeOrigen];
            var estadoFiltro = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
            if (estadoFiltro is not null && !Estados.Contains(estadoFiltro)) errores["estado"] = [MensajeEstado];
            // CA-P7-05: el asesor no filtra por asesor; el parámetro se ignora (aunque no sea válido).
            var asesor = actor.EsAdmin ? LeerEntero(asesorId, 1, long.MaxValue, "asesorId", MensajeAsesor, errores) : null;
            var numeroPagina = (int)(LeerEntero(pagina, 1, int.MaxValue, "pagina", MensajePagina, errores) ?? 1);
            var tamanoPagina = (int)(LeerEntero(tamano, 1, TamanoMaximo, "tamano", MensajePagina, errores) ?? TamanoPorDefecto);
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(MensajesP7.Filtros, errores));

            var consulta = Alcance(db, actor);
            if (fechaDesde is not null) consulta = consulta.Where(x => x.Venta.FechaVenta >= fechaDesde);
            if (fechaHasta is not null) consulta = consulta.Where(x => x.Venta.FechaVenta <= fechaHasta);
            if (origenFiltro is not null) consulta = consulta.Where(x => x.Venta.Origen == origenFiltro);
            if (estadoFiltro == EstadoVigente) consulta = consulta.Where(x => x.Venta.RevertidaEn == null);
            if (estadoFiltro == EstadoRevertida) consulta = consulta.Where(x => x.Venta.RevertidaEn != null);
            if (asesor is not null) consulta = consulta.Where(x => x.Vehiculo.AsesorId == asesor);

            var total = await consulta.CountAsync(ct);
            var saltar = (long)(numeroPagina - 1) * tamanoPagina;
            var items = saltar >= total
                ? []
                : await ItemsAsync(db, actor, consulta.OrderByDescending(x => x.Venta.RegistradaEn)
                    .ThenByDescending(x => x.Venta.Id).Skip((int)saltar).Take(tamanoPagina), ct);
            var ordenados = items.OrderByDescending(i => i.RegistradaEn).ThenByDescending(i => i.Id).ToArray();
            return Results.Ok(new VentasPaginaResponse(ordenados, total, numeroPagina, tamanoPagina));
        }).RequireAuthorization(Roles.PoliticaAsesorOAdministrador);

        // CA-P7-06, CA-P7-07, CA-P7-09: todas las ventas de un vehículo, la vigente primero.
        app.MapGet("/api/inventario/vehiculos/{id}/ventas", async (long id, ClaimsPrincipal principal, CamionDbContext db,
            CancellationToken ct) =>
        {
            var actor = await InventarioEndpoints.ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();
            if (!await InventarioEndpoints.Alcance(db.Vehiculos.AsNoTracking(), actor).AnyAsync(v => v.Id == id, ct))
                return Results.NotFound(new ErrorResponse(ReglasP4b.MensajeVehiculoNoEncontrado));
            var items = await ItemsAsync(db, actor, Alcance(db, actor).Where(x => x.Venta.VehiculoId == id), ct);
            return Results.Ok(new VentasVehiculoResponse(items
                .OrderBy(i => i.Estado == EstadoVigente ? 0 : 1)
                .ThenByDescending(i => i.RegistradaEn).ThenByDescending(i => i.Id).ToArray()));
        }).RequireAuthorization(Roles.PoliticaAsesorOAdministrador);

        return app;
    }

    private sealed class Fila
    {
        public VentaVehiculo Venta { get; init; } = null!;
        public Vehiculo Vehiculo { get; init; } = null!;
    }

    private static IQueryable<Fila> Alcance(CamionDbContext db, InventarioEndpoints.Actor actor) =>
        from venta in db.VentasVehiculo.AsNoTracking()
        join v in InventarioEndpoints.Alcance(db.Vehiculos.AsNoTracking(), actor) on venta.VehiculoId equals v.Id
        select new Fila { Venta = venta, Vehiculo = v };

    private static async Task<List<VentaItem>> ItemsAsync(CamionDbContext db, InventarioEndpoints.Actor actor,
        IQueryable<Fila> consulta, CancellationToken ct)
    {
        var filas = await consulta.Select(x => new
        {
            x.Venta,
            x.Vehiculo.Id,
            x.Vehiculo.Estado,
            x.Vehiculo.AsesorId,
            x.Vehiculo.ModeloAnio,
            Tipo = x.Vehiculo.TipoVehiculo!.Nombre,
            Marca = x.Vehiculo.Marca == null ? null : x.Vehiculo.Marca.Nombre,
            Linea = x.Vehiculo.Linea == null ? null : x.Vehiculo.Linea.Nombre,
            AsesorNombre = db.PerfilesAsesor.Where(a => a.Id == x.Vehiculo.AsesorId)
                .Select(a => db.Users.Where(u => u.Id == a.UsuarioId).Select(u => u.NombreCompleto).FirstOrDefault())
                .FirstOrDefault()
        }).ToListAsync(ct);

        var ids = filas.SelectMany(f => new[] { f.Venta.RegistradaPor, f.Venta.RevertidaPor }).OfType<string>().Distinct().ToList();
        var nombres = ids.Count == 0
            ? new Dictionary<string, string>()
            : await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.NombreCompleto, ct);

        return filas.Select(f => new VentaItem(
            f.Venta.Id,
            new VehiculoVenta(f.Id, ReglasP4b.TituloVehiculo(f.Marca, f.Linea, f.ModeloAnio, f.Tipo), f.Estado),
            actor.EsAdmin ? new AsesorVenta(f.AsesorId, f.AsesorNombre ?? "") : null,
            f.Venta.FechaVenta,
            ConsultasCatalogo.Pesos(f.Venta.PrecioFinalCop),
            f.Venta.Origen,
            f.Venta.Observaciones,
            f.Venta.RevertidaEn is null ? EstadoVigente : EstadoRevertida,
            ConsultasCatalogo.Utc(f.Venta.RegistradaEn),
            f.Venta.RegistradaPor is { } r ? nombres.GetValueOrDefault(r) : null,
            f.Venta.RevertidaEn is { } re ? ConsultasCatalogo.Utc(re) : null,
            f.Venta.RevertidaPor is { } rp ? nombres.GetValueOrDefault(rp) : null,
            f.Venta.MotivoReversion)).ToList();
    }

    private static DateOnly? LeerFecha(string? valor, string campo, Dictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        if (DateOnly.TryParseExact(valor.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
            return f;
        errores[campo] = [MensajeFecha];
        return null;
    }

    private static long? LeerEntero(string? valor, long minimo, long maximo, string campo, string mensaje,
        Dictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        if (long.TryParse(valor.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= minimo && n <= maximo)
            return n;
        errores[campo] = [mensaje];
        return null;
    }

    private static IResult SinPermiso() =>
        Results.Json(new ErrorResponse(MensajesError.SinPermiso), statusCode: StatusCodes.Status403Forbidden);
}
