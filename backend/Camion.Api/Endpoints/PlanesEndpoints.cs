using System.Globalization;
using System.Security.Claims;
using Camion.Api.Auth;
using Camion.Api.Catalogo;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Models;
using Camion.Api.Suscripciones;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Endpoints;

// P4p: planes (HU-049 crear, HU-050 modificar, HU-051 activar/inactivar; RN-PL-001, RN-PL-002, D-26, D-35).
// Solo el administrador. Un plan nunca se borra; editarlo no cambia las suscripciones en curso.
public static class PlanesEndpoints
{
    public static IEndpointRouteBuilder MapPlanesEndpoints(this IEndpointRouteBuilder app)
    {
        var planes = app.MapGroup("/api/admin/planes").RequireAuthorization(Roles.PoliticaSoloAdministrador);

        planes.MapGet("", async (string? activo, CamionDbContext db, CancellationToken ct) =>
        {
            bool? filtro = null;
            if (!string.IsNullOrWhiteSpace(activo))
            {
                if (!bool.TryParse(activo.Trim(), out var valor))
                    return Results.BadRequest(new ErrorResponse(ReglasP4p.MensajeFiltros,
                        new Dictionary<string, string[]> { ["activo"] = [ReglasP4p.MensajeActivo] }));
                filtro = valor;
            }
            var consulta = db.Planes.AsNoTracking();
            if (filtro is not null) consulta = consulta.Where(p => p.Activo == filtro);
            var lista = await consulta.ToListAsync(ct);
            var vivas = await VivasPorPlanAsync(db, lista.Select(p => p.Id).ToList(), ct);
            var comparador = StringComparer.Create(CultureInfo.GetCultureInfo("es-CO"), CompareOptions.IgnoreCase);
            var items = lista
                .OrderByDescending(p => p.Activo).ThenBy(p => p.Nombre, comparador).ThenBy(p => p.Id)
                .Select(p => Item(p, vivas.GetValueOrDefault(p.Id)))
                .ToArray();
            return Results.Ok(new PlanesResponse(items));
        });

        // HU-049
        planes.MapPost("", async (PlanRequest? request, ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj,
            CancellationToken ct) =>
        {
            var errores = ReglasP4p.ValidarPlan(request);
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasP4p.MensajeRevisar, errores));
            var nombre = ReglasP4p.NormalizarTexto(request!.Nombre);
            if (await NombreOcupadoAsync(db, nombre, null, ct))
                return Results.Conflict(new ErrorResponse(ReglasP4p.MensajePlanDuplicado));

            var ahora = reloj.GetUtcNow().UtcDateTime;
            var plan = new Plan
            {
                Nombre = nombre,
                PrecioCop = request.PrecioCop!.Value,
                DuracionMeses = (int)request.DuracionMeses!.Value,
                LimitePublicaciones = (int)request.LimitePublicaciones!.Value,
                EsPrueba = request.EsPrueba ?? false,
                Activo = true,
                CreadoEn = ahora
            };
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Planes.Add(plan);
            await db.SaveChangesAsync(ct);
            Auditoria.AgregarEvento(db, principal.FindFirstValue(ClaimTypes.NameIdentifier), Auditoria.EntidadPlan,
                Id(plan.Id), Auditoria.Crear, null, Datos(plan), ahora);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/admin/planes/{plan.Id}", Item(plan, 0));
        });

        // HU-050 (RN-PL-001): las suscripciones en curso no cambian; el plan nuevo se aplica al asignar o renovar.
        planes.MapPut("/{id}", async (long id, PlanRequest? request, ClaimsPrincipal principal, CamionDbContext db,
            TimeProvider reloj, CancellationToken ct) =>
        {
            var errores = ReglasP4p.ValidarPlan(request);
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasP4p.MensajeRevisar, errores));
            var plan = await db.Planes.SingleOrDefaultAsync(p => p.Id == id, ct);
            if (plan is null) return NoEncontrado();
            var nombre = ReglasP4p.NormalizarTexto(request!.Nombre);
            if (await NombreOcupadoAsync(db, nombre, id, ct))
                return Results.Conflict(new ErrorResponse(ReglasP4p.MensajePlanDuplicado));

            var antes = Datos(plan);
            plan.Nombre = nombre;
            plan.PrecioCop = request.PrecioCop!.Value;
            plan.DuracionMeses = (int)request.DuracionMeses!.Value;
            plan.LimitePublicaciones = (int)request.LimitePublicaciones!.Value;
            plan.EsPrueba = request.EsPrueba ?? false;
            var despues = Datos(plan);
            var cambios = despues.Keys.Where(k => !Equals(antes[k], despues[k])).ToList();
            if (cambios.Count > 0)
            {
                Auditoria.AgregarEvento(db, principal.FindFirstValue(ClaimTypes.NameIdentifier), Auditoria.EntidadPlan,
                    Id(plan.Id), Auditoria.Editar, cambios.ToDictionary(k => k, k => antes[k]),
                    cambios.ToDictionary(k => k, k => despues[k]), reloj.GetUtcNow().UtcDateTime);
                await db.SaveChangesAsync(ct);
            }
            var vivas = await VivasPorPlanAsync(db, [plan.Id], ct);
            return Results.Ok(Item(plan, vivas.GetValueOrDefault(plan.Id)));
        });

        // HU-051 (RN-PL-002): inactivar impide asignarlo o renovarlo con él; las suscripciones que ya lo usan siguen.
        foreach (var (ruta, activo, accion) in new[] { ("activar", true, Auditoria.Activar), ("inactivar", false, Auditoria.Inactivar) })
        {
            planes.MapPost("/{id}/" + ruta, async (long id, ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj,
                CancellationToken ct) =>
            {
                var plan = await db.Planes.SingleOrDefaultAsync(p => p.Id == id, ct);
                if (plan is null) return NoEncontrado();
                if (plan.Activo != activo)
                {
                    plan.Activo = activo;
                    Auditoria.AgregarEvento(db, principal.FindFirstValue(ClaimTypes.NameIdentifier), Auditoria.EntidadPlan,
                        Id(plan.Id), accion, new Dictionary<string, object?> { ["activo"] = !activo },
                        new Dictionary<string, object?> { ["activo"] = activo }, reloj.GetUtcNow().UtcDateTime);
                    await db.SaveChangesAsync(ct);
                }
                var vivas = await VivasPorPlanAsync(db, [plan.Id], ct);
                return Results.Ok(Item(plan, vivas.GetValueOrDefault(plan.Id)));
            });
        }

        return app;
    }

    private static IResult NoEncontrado() => Results.NotFound(new ErrorResponse(ReglasP4p.MensajeNoEncontrado));

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    // Unicidad sin distinguir mayúsculas, comparada en C# (igual en SQLite y MariaDB). La tabla planes es pequeña.
    private static async Task<bool> NombreOcupadoAsync(CamionDbContext db, string nombre, long? excepto, CancellationToken ct)
    {
        var clave = ReglasP4p.ClavePlan(nombre);
        var nombres = await db.Planes.AsNoTracking().Where(p => excepto == null || p.Id != excepto)
            .Select(p => p.Nombre).ToListAsync(ct);
        return nombres.Any(n => ReglasP4p.ClavePlan(n) == clave);
    }

    private static async Task<Dictionary<long, int>> VivasPorPlanAsync(CamionDbContext db, List<long> ids, CancellationToken ct) =>
        await db.Suscripciones.AsNoTracking()
            .Where(s => ids.Contains(s.PlanId) && Suscripcion.EstadosVivos.Contains(s.Estado))
            .GroupBy(s => s.PlanId).Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);

    public static PlanItem Item(Plan p, int vivas) => new(
        p.Id, p.Nombre, ConsultasCatalogo.Pesos(p.PrecioCop), p.DuracionMeses, ReglasP4p.DiasVigencia(p.DuracionMeses),
        p.LimitePublicaciones, p.EsPrueba, p.Activo, vivas);

    private static Dictionary<string, object?> Datos(Plan p) => new()
    {
        ["nombre"] = p.Nombre,
        ["precioCop"] = ConsultasCatalogo.Pesos(p.PrecioCop),
        ["duracionMeses"] = p.DuracionMeses,
        ["limitePublicaciones"] = p.LimitePublicaciones,
        ["esPrueba"] = p.EsPrueba,
        ["activo"] = p.Activo
    };
}
