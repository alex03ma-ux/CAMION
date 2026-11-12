using System.Globalization;
using System.Security.Claims;
using Camion.Api.Auth;
using Camion.Api.Catalogo;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Leads;
using Camion.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Endpoints;

// P6: "Mis solicitudes" del comprador (D-53, D-59; CA-P6-66 a CA-P6-76). Contrato: docs/criterios/P6-contrato-api.md.
// Solo el rol COMPRADOR (asesor y administrador 403; sin token 401). Solo lectura. Solo los leads enviados con su sesión
// (usuario_id), de los cinco orígenes. Nunca muestra notas, historial, responsable, consentimientos, resultado, observación
// de cierre ni los datos de contacto o el mensaje (CA-P6-68).
public static class MisSolicitudesEndpoints
{
    private const int TamanoPorDefecto = 20;
    private const int TamanoMaximo = 50;

    public const string EstadoPendiente = "PENDIENTE";
    public const string EstadoFinalizada = "FINALIZADA";
    public const string EstadoRegistrada = "REGISTRADA";
    public const string EstadoEnGestion = "EN_GESTION";
    public const string ResumenSinVehiculo = "Sin vehículo asociado";
    public const string ResumenVende = "Venta de tu vehículo";

    public static IEndpointRouteBuilder MapMisSolicitudesEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/mis-solicitudes").RequireAuthorization(Roles.PoliticaSoloComprador);

        // CA-P6-66, CA-P6-67: una sola lista, de la más reciente a la más antigua.
        grupo.MapGet("", async (string? pagina, string? tamano, ClaimsPrincipal principal, CamionDbContext db,
            TimeProvider reloj, CancellationToken ct) =>
        {
            var errores = new Dictionary<string, string[]>();
            var numeroPagina = (int)(LeerEntero(pagina, 1, int.MaxValue, "pagina", errores) ?? 1);
            var tamanoPagina = (int)(LeerEntero(tamano, 1, TamanoMaximo, "tamano", errores) ?? TamanoPorDefecto);
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasLeads.MensajeFiltros, errores));

            var usuarioId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var consulta = db.Leads.AsNoTracking().Where(l => l.UsuarioId == usuarioId);
            var total = await consulta.CountAsync(ct);
            var saltar = (long)(numeroPagina - 1) * tamanoPagina;
            var filas = saltar >= total
                ? []
                : await Proyectar(db, consulta.OrderByDescending(l => l.CreadoEn).ThenByDescending(l => l.Id)
                    .Skip((int)saltar).Take(tamanoPagina)).ToListAsync(ct);
            var items = await ItemsAsync(db, filas, reloj.GetUtcNow().UtcDateTime, ct);
            return Results.Ok(new MisSolicitudesPaginaResponse(items, total, numeroPagina, tamanoPagina));
        });

        // CA-P6-72, CA-P6-73: la misma información de una solicitud propia; la ajena o inexistente da 404.
        grupo.MapGet("/{id}", async (long id, ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj,
            CancellationToken ct) =>
        {
            var usuarioId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var fila = await Proyectar(db, db.Leads.AsNoTracking().Where(l => l.Id == id && l.UsuarioId == usuarioId))
                .FirstOrDefaultAsync(ct);
            if (fila is null) return Results.NotFound(new ErrorResponse(ReglasLeads.MensajeSolicitudNoEncontrada));
            return Results.Ok((await ItemsAsync(db, [fila], reloj.GetUtcNow().UtcDateTime, ct))[0]);
        });

        return app;
    }

    private static IQueryable<Fila> Proyectar(CamionDbContext db, IQueryable<Lead> consulta) =>
        consulta.Select(l => new Fila
        {
            Id = l.Id,
            CreadoEn = l.CreadoEn,
            Origen = l.Origen,
            Estado = l.Estado,
            TipoVehiculo = l.TipoVehiculo,
            PublicacionId = l.PublicacionId,
            Titulo = l.Publicacion == null ? null : l.Publicacion.Titulo,
            Slug = l.Publicacion == null ? null : l.Publicacion.SlugPublico,
            ConNotas = db.EventosLead.Any(e => e.LeadId == l.Id && e.Tipo == EventoLead.Nota)
        });

    private static async Task<IReadOnlyList<MiSolicitudItem>> ItemsAsync(CamionDbContext db, IReadOnlyList<Fila> filas,
        DateTime ahora, CancellationToken ct)
    {
        if (filas.Count == 0) return [];
        // CA-P6-71: solo se enlaza la ficha de una publicación visible hoy.
        var publicaciones = filas.Select(f => f.PublicacionId).OfType<long>().Distinct().ToList();
        var visibles = publicaciones.Count == 0
            ? []
            : await ConsultasCatalogo.Visibles(db, ahora).Where(p => publicaciones.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);

        // CA-P6-72: vehículos relacionados vigentes y visibles de cada búsqueda, sin la nota interna.
        var busquedas = filas.Where(f => f.Origen == Lead.OrigenBusqueda).Select(f => f.Id).ToList();
        var criterios = await ReglasBusqueda.CriteriosAsync(db, busquedas, ct);
        var relaciones = busquedas.Count == 0
            ? []
            : await db.BusquedaVehiculos.AsNoTracking()
                .Where(r => busquedas.Contains(r.LeadId) && r.RetiradoEn == null)
                .OrderByDescending(r => r.RelacionadoEn).ThenByDescending(r => r.Id)
                .Select(r => new { r.LeadId, r.PublicacionId }).ToListAsync(ct);
        var idsRelacionados = relaciones.Select(r => r.PublicacionId).Distinct().ToList();
        var tarjetas = idsRelacionados.Count == 0
            ? []
            : (await ConsultasCatalogo.ATarjetas(ConsultasCatalogo.VisiblesConDestacado(db, ahora)
                    .Where(x => idsRelacionados.Contains(x.Publicacion.Id))).ToListAsync(ct))
                .ToDictionary(t => t.Id, ConsultasCatalogo.Tarjeta);

        return filas.Select(f =>
        {
            var conVehiculo = f.PublicacionId is { } pid;
            var disponible = conVehiculo && visibles.Contains(f.PublicacionId!.Value);
            var vehiculo = conVehiculo ? new MiSolicitudVehiculo(f.Titulo ?? "", disponible ? f.Slug : null, disponible) : null;
            IReadOnlyList<TarjetaVehiculo> recomendados = f.Origen == Lead.OrigenBusqueda
                ? relaciones.Where(r => r.LeadId == f.Id && tarjetas.ContainsKey(r.PublicacionId))
                    .Select(r => tarjetas[r.PublicacionId]).ToArray()
                : [];
            return new MiSolicitudItem(f.Id, ConsultasCatalogo.Utc(f.CreadoEn), f.Origen,
                Resumen(f, criterios.GetValueOrDefault(f.Id)), Estado(f), vehiculo, recomendados);
        }).ToArray();
    }

    // CA-P6-66: título del vehículo, tipo de vehículo del seguro o categoría buscada.
    private static string Resumen(Fila f, BusquedaCriterios? criterios) => f.Origen switch
    {
        Lead.OrigenContactoVehiculo => f.Titulo ?? "",
        Lead.OrigenCredito => f.Titulo ?? ResumenSinVehiculo,
        Lead.OrigenSeguroTodoRiesgo => ReglasLeads.EtiquetaTipoVehiculo(f.TipoVehiculo),
        Lead.OrigenBusqueda => criterios is null ? ReglasLeads.MensajeBusqueda : ReglasBusqueda.Titulo(criterios),
        _ => ResumenVende
    };

    // CA-P6-70 (D-57 a A, D-59 b A): sin el resultado de cierre.
    private static string Estado(Fila f) =>
        f.Estado == Lead.Cerrado ? EstadoFinalizada
        : !Lead.TieneEstadoGestion(f.Origen) ? EstadoPendiente
        : f.ConNotas ? EstadoEnGestion
        : EstadoRegistrada;

    private static long? LeerEntero(string? valor, long minimo, long maximo, string parametro,
        Dictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        if (!long.TryParse(valor.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var numero) ||
            numero < minimo || numero > maximo)
        {
            errores[parametro] = [ReglasLeads.MensajePagina];
            return null;
        }
        return numero;
    }

    private sealed class Fila
    {
        public long Id { get; init; }
        public DateTime CreadoEn { get; init; }
        public string Origen { get; init; } = "";
        public string Estado { get; init; } = "";
        public string? TipoVehiculo { get; init; }
        public long? PublicacionId { get; init; }
        public string? Titulo { get; init; }
        public string? Slug { get; init; }
        public bool ConNotas { get; init; }
    }
}
