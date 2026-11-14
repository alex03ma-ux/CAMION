using System.Security.Claims;
using Camion.Api.Auth;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Leads;
using Camion.Api.Models;
using Camion.Api.Reportes;

namespace Camion.Api.Endpoints;

// P8 (HU-075 a HU-080, RN-RE-001 a RN-RE-006, D-68 A, D-69 A, D-70 A, D-71): reportes, exportación CSV e indicadores del
// administrador, y "Mis indicadores" del asesor. Todas GET y de solo lectura (las que cuentan solicitudes barren antes,
// D-46). Contrato: docs/criterios/P8-contrato-api.md.
public static class ReportesEndpoints
{
    public const int MaxFilasCsv = 10_000;

    private sealed record DefinicionReporte(
        string Tipo,
        UsaFiltros Filtros,
        bool Barrer,
        string? OrigenFijo,
        Func<CamionDbContext, FiltrosReporte, DateTime, CancellationToken, Task<IReporte>> Calcular);

    private static readonly DefinicionReporte[] Reportes =
    [
        new(ConsultasReportes.TipoVehiculos, UsaFiltros.Asesor | UsaFiltros.Categoria | UsaFiltros.Ubicacion | UsaFiltros.Paginacion,
            false, null, (db, f, ahora, ct) => ConsultasReportes.VehiculosAsync(db, f, ahora, ct)),
        new(ConsultasReportes.TipoSolicitudes, UsaFiltros.OrigenLead | UsaFiltros.EstadoLead | UsaFiltros.Resultado |
                UsaFiltros.Responsable | UsaFiltros.Categoria | UsaFiltros.Paginacion,
            true, null, (db, f, _, ct) => ConsultasReportes.SolicitudesAsync(db, f, ct)),
        new(ConsultasReportes.TipoCreditos, UsaFiltros.EstadoCredito | UsaFiltros.Resultado | UsaFiltros.Canal | UsaFiltros.Paginacion,
            true, Lead.OrigenCredito, (db, f, _, ct) => ConsultasReportes.CreditosAsync(db, f, ct)),
        new(ConsultasReportes.TipoVentas, UsaFiltros.Asesor | UsaFiltros.Categoria | UsaFiltros.OrigenVenta | UsaFiltros.Paginacion,
            false, null, (db, f, _, ct) => ConsultasReportes.VentasReporteAsync(db, f, ct)),
        new(ConsultasReportes.TipoSuscripciones, UsaFiltros.Asesor | UsaFiltros.Plan | UsaFiltros.Medio | UsaFiltros.Paginacion,
            false, null, (db, f, ahora, ct) => ConsultasReportes.SuscripcionesAsync(db, f, ahora, ct)),
    ];

    public static IEndpointRouteBuilder MapReportesEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(Roles.PoliticaSoloAdministrador);

        foreach (var def in Reportes)
        {
            // CA-P8-01 a CA-P8-33: el reporte en pantalla (cifras y filas paginadas).
            admin.MapGet("/reportes/" + def.Tipo, async (HttpRequest http, CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
            {
                var ahora = reloj.GetUtcNow().UtcDateTime;
                var (f, errores) = await FiltrosReporte.LeerAsync(http.Query, db, ahora, def.Filtros, ct, def.OrigenFijo);
                if (f is null) return Results.BadRequest(new ErrorResponse(MensajesP8.Filtros, errores));
                if (def.Barrer) await BarridoLeads.BarrerAsync(db, ahora, ct);
                return Results.Ok((await def.Calcular(db, f, ahora, ct)).Respuesta);
            });

            // CA-P8-42 a CA-P8-50: el mismo reporte en CSV, con todas las filas, auditado y sin guardar nada en disco.
            admin.MapGet("/reportes/" + def.Tipo + "/csv", async (HttpRequest http, ClaimsPrincipal principal, CamionDbContext db,
                TimeProvider reloj, ILoggerFactory loggerFactory, CancellationToken ct) =>
            {
                var ahora = reloj.GetUtcNow().UtcDateTime;
                var (f, errores) = await FiltrosReporte.LeerAsync(http.Query, db, ahora, def.Filtros & ~UsaFiltros.Paginacion, ct,
                    def.OrigenFijo);
                if (f is null) return Results.BadRequest(new ErrorResponse(MensajesP8.Filtros, errores));
                if (def.Barrer) await BarridoLeads.BarrerAsync(db, ahora, ct);
                var reporte = await def.Calcular(db, f, ahora, ct);
                if (reporte.TotalFilas > MaxFilasCsv) return Results.BadRequest(new ErrorResponse(MensajesP8.DemasiadasFilas));

                var csv = new Csv();
                reporte.EscribirCsv(csv);
                var adminId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                Auditoria.AgregarEvento(db, adminId, Auditoria.EntidadReporte, def.Tipo, Auditoria.Exportar, null,
                    new Dictionary<string, object?>
                    {
                        ["reporte"] = def.Tipo, ["filtros"] = f.ParaAuditoria(), ["filas"] = reporte.TotalFilas
                    }, ahora);
                await db.SaveChangesAsync(ct);
                loggerFactory.CreateLogger(AuthEndpoints.CategoriaAuditoria).LogInformation(
                    "Auditoria reporte_exportar reporte={Reporte} filas={Filas} administradorId={AdministradorId}",
                    def.Tipo, reporte.TotalFilas, adminId);

                http.HttpContext.Response.Headers.CacheControl = "no-store";
                var nombre = $"reporte-{def.Tipo}-{Etiquetas.Fecha(f.Desde)}_{Etiquetas.Fecha(f.Hasta)}.csv";
                return Results.File(csv.Bytes(), "text/csv; charset=utf-8", nombre);
            });
        }

        // CA-P8-34 a CA-P8-41 (D-69 A/A): indicadores del período, comparación con el anterior y datos de 4 gráficos.
        admin.MapGet("/indicadores", async (HttpRequest http, CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var ahora = reloj.GetUtcNow().UtcDateTime;
            var (f, errores) = await FiltrosReporte.LeerAsync(http.Query, db, ahora, UsaFiltros.Ninguno, ct);
            if (f is null) return Results.BadRequest(new ErrorResponse(MensajesP8.Filtros, errores));
            await BarridoLeads.BarrerAsync(db, ahora, ct);
            return Results.Ok(await ConsultasIndicadores.AdminAsync(db, f, ahora, ct));
        });

        // CA-P8-51 a CA-P8-55 (D-70 A): "Mis indicadores", solo lo propio y sin exportar. asesorId se ignora.
        app.MapGet("/api/asesor/indicadores", async (HttpRequest http, ClaimsPrincipal principal, CamionDbContext db,
            TimeProvider reloj, CancellationToken ct) =>
        {
            var actor = await InventarioEndpoints.ActorAsync(principal, db, ct);
            if (actor is null || actor.EsAdmin || actor.PerfilAsesorId is not { } perfil)
                return Results.Json(new ErrorResponse(MensajesError.SinPermiso), statusCode: StatusCodes.Status403Forbidden);
            var ahora = reloj.GetUtcNow().UtcDateTime;
            var (f, errores) = await FiltrosReporte.LeerAsync(http.Query, db, ahora, UsaFiltros.Ninguno, ct);
            if (f is null) return Results.BadRequest(new ErrorResponse(MensajesP8.Filtros, errores));
            await BarridoLeads.BarrerAsync(db, ahora, ct);
            return Results.Ok(await ConsultasIndicadores.AsesorAsync(db, perfil, f, ahora, ct));
        }).RequireAuthorization(Roles.PoliticaSoloAsesor);

        return app;
    }
}
