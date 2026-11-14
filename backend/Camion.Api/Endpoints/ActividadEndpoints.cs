using System.Globalization;
using Camion.Api.Auth;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Reportes;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Endpoints;

// P8 (HU-061, RN-SE-003, D-62 d, D-72 A/A): "Actividad" de un vehículo y de una suscripción. Solo el administrador, solo
// GET. La ruta se llama /actividad (sin "auditoria" ni "historial") para que CA-061-15 de SesionTests siga vigente.
public static class ActividadEndpoints
{
    public static IEndpointRouteBuilder MapActividadEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(Roles.PoliticaSoloAdministrador);

        // CA-P8-56, CA-P8-58 a CA-P8-62.
        admin.MapGet("/vehiculos/{id}/actividad", async (long id, string? pagina, string? tamano, CamionDbContext db,
            CancellationToken ct) =>
        {
            if (!LeerPagina(pagina, tamano, out var p, out var t, out var error)) return error!;
            if (!await db.Vehiculos.AsNoTracking().AnyAsync(v => v.Id == id, ct))
                return Results.NotFound(new ErrorResponse(MensajesP8.VehiculoNoEncontrado));
            return Results.Ok(await ConsultasActividad.VehiculoAsync(db, id, p, t, ct));
        });

        // CA-P8-57 a CA-P8-62.
        admin.MapGet("/suscripciones/{id}/actividad", async (long id, string? pagina, string? tamano, CamionDbContext db,
            CancellationToken ct) =>
        {
            if (!LeerPagina(pagina, tamano, out var p, out var t, out var error)) return error!;
            if (!await db.Suscripciones.AsNoTracking().AnyAsync(s => s.Id == id, ct))
                return Results.NotFound(new ErrorResponse(MensajesP8.SuscripcionNoEncontrada));
            return Results.Ok(await ConsultasActividad.SuscripcionAsync(db, id, p, t, ct));
        });

        return app;
    }

    private static bool LeerPagina(string? pagina, string? tamano, out int p, out int t, out IResult? error)
    {
        var errores = new Dictionary<string, string[]>();
        p = Leer(pagina, int.MaxValue, "pagina", errores) ?? 1;
        t = Leer(tamano, FiltrosReporte.TamanoMaximo, "tamano", errores) ?? FiltrosReporte.TamanoPorDefecto;
        error = errores.Count > 0 ? Results.BadRequest(new ErrorResponse(MensajesP8.Filtros, errores)) : null;
        return error is null;
    }

    private static int? Leer(string? valor, int maximo, string campo, Dictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        if (int.TryParse(valor.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 1 && n <= maximo) return n;
        errores[campo] = [MensajesP8.Pagina];
        return null;
    }
}
