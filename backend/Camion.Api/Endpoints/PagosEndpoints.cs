using System.Globalization;
using System.Security.Claims;
using Camion.Api.Auth;
using Camion.Api.Catalogo;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Models;
using Camion.Api.Leads;
using Camion.Api.Publicaciones;
using Camion.Api.Suscripciones;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Endpoints;

// P7 (D-61 B, FN-04): registro interno de los pagos del plan. El cobro es fuera del portal; solo el administrador ve y
// anota pagos, y puede anularlos con motivo (la fila nunca se borra). Sin comprobante, pasarela, factura ni contabilidad.
// Asignar, renovar y cambiar de plan con pago están en SuscripcionesEndpoints (misma transacción).
public static class PagosEndpoints
{
    public static IEndpointRouteBuilder MapPagosEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization(Roles.PoliticaSoloAdministrador);

        // CA-P7-30: historial de pagos de una suscripción y total pagado (solo los REGISTRADO).
        admin.MapGet("/suscripciones/{id}/pagos", async (long id, CamionDbContext db, CancellationToken ct) =>
        {
            if (!await db.Suscripciones.AsNoTracking().AnyAsync(s => s.Id == id, ct))
                return Results.NotFound(new ErrorResponse(ReglasP4p.MensajeNoEncontrado));
            var items = await ItemsAsync(db, db.Pagos.AsNoTracking().Where(p => p.SuscripcionId == id), ct);
            var total = items.Where(p => p.Estado == Pago.Registrado).Sum(p => p.ValorCop);
            return Results.Ok(new PagosResponse(items, total));
        });

        // PROPUESTO (contrato P7 §2): pago anotado después de asignar o renovar, p. ej. tras anular uno mal escrito.
        admin.MapPost("/suscripciones/{id}/pagos", async (long id, PagoRequest? request, ClaimsPrincipal principal,
            CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var ahora = reloj.GetUtcNow().UtcDateTime;
            var plan = await (from s in db.Suscripciones.AsNoTracking()
                              join p in db.Planes on s.PlanId equals p.Id
                              where s.Id == id
                              select p).FirstOrDefaultAsync(ct);
            if (plan is null) return Results.NotFound(new ErrorResponse(ReglasP4p.MensajeNoEncontrado));
            var errores = ReglasPagos.Validar(request ?? new PagoRequest(null, null, null, null), ahora, out var datos);
            if (errores.Count > 0 || datos is null)
                return Results.BadRequest(new ErrorResponse(ReglasP4p.MensajeRevisar, errores));
            if (plan.PrecioCop <= 0)
                return Results.BadRequest(new ErrorResponse(ReglasP4p.MensajeRevisar,
                    new Dictionary<string, string[]> { ["pago"] = [ReglasPagos.MensajePlanSinPrecio] }));

            var adminId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var nuevo = await SuscripcionesEndpoints.AnotarPagoAsync(db, id, datos, plan, Pago.ConceptoPosterior, adminId, ahora, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/admin/suscripciones/{id}/pagos", await ItemAsync(db, nuevo!.Id, ct));
        });

        // D-67: qué hará la anulación, sin cambiar nada. bloqueadoPor != null -> la anulación responde 409 con mensaje.
        admin.MapGet("/pagos/{id}/efecto-anulacion", async (long id, CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var plan = await AnulacionPagos.PlanearAsync(db, id, reloj.GetUtcNow().UtcDateTime, ct);
            return plan is null ? Results.NotFound(new ErrorResponse(ReglasPagos.MensajeNoEncontrado)) : Results.Ok(Efecto(plan));
        });

        // CA-P7-31, D-67: anular con motivo (5 a 500). Queda ANULADO con quién, cuándo y por qué, y en la misma transacción
        // se deshace lo que dio el pago (renovación, asignación o cambio de plan; ver Suscripciones/AnulacionPagos.cs).
        admin.MapPost("/pagos/{id}/anular", async (long id, AnularPagoRequest? request, ClaimsPrincipal principal,
            CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            if (!await db.Pagos.AsNoTracking().AnyAsync(p => p.Id == id, ct))
                return Results.NotFound(new ErrorResponse(ReglasPagos.MensajeNoEncontrado));
            var errores = ValidacionAuth.ValidarMotivo(request?.Motivo, obligatorio: true);
            // B-3: sin saltos de línea ni caracteres de control (los espacios de los extremos se quitan antes).
            if (errores.Count == 0 && ValidacionAuth.TieneControl(request!.Motivo!.Trim()))
                errores["motivo"] = [ReglasPagos.MensajeMotivoControl];
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasP4p.MensajeRevisar, errores));
            var motivo = ValidacionAuth.NormalizarOpcional(request!.Motivo)!;
            var adminId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var ahora = reloj.GetUtcNow().UtcDateTime;

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Bloquea al asesor (como las demás acciones de suscripciones) y planea dentro de la transacción.
            var asesorId = await db.Pagos.AsNoTracking().Where(p => p.Id == id)
                .Join(db.Suscripciones, p => p.SuscripcionId, s => s.Id, (p, s) => s.AsesorId).SingleAsync(ct);
            await DatosP4b.BloquearAsesorAsync(db, asesorId, ct);
            var plan = (await AnulacionPagos.PlanearAsync(db, id, ahora, ct))!;
            // B-4: se audita el estado real anterior. Sin fecha o medio (script 002) o con un movimiento posterior: 409.
            if (plan.Bloqueo is not null) return Results.Conflict(new ErrorResponse(plan.Mensaje!));
            var estadoAnterior = plan.Pago.Estado;
            // UPDATE condicionado al estado leído: de dos anulaciones simultáneas solo una cambia la fila (la otra recibe 409).
            var cambiadas = await db.Pagos.Where(p => p.Id == id && p.Estado == estadoAnterior)
                .ExecuteUpdateAsync(x => x
                    .SetProperty(p => p.Estado, Pago.Anulado)
                    .SetProperty(p => p.AnuladoPor, adminId)
                    .SetProperty(p => p.AnuladoEn, ahora)
                    .SetProperty(p => p.MotivoAnulacion, motivo), ct);
            if (cambiadas == 0) return Results.Conflict(new ErrorResponse(ReglasPagos.MensajeYaAnulado));
            await AnulacionPagos.AplicarAsync(db, plan, adminId, motivo, ahora, ct);
            Auditoria.AgregarEvento(db, adminId, Auditoria.EntidadPago, id.ToString(CultureInfo.InvariantCulture),
                Auditoria.Anular, new Dictionary<string, object?> { ["estado"] = estadoAnterior },
                new Dictionary<string, object?>
                {
                    ["estado"] = Pago.Anulado, ["motivo"] = motivo, ["efecto"] = plan.Tipo, ["suscripcionId"] = plan.SuscripcionId
                }, ahora);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            // D-41, D-46: si la suscripción dejó de estar vigente, sus publicaciones dejan de verse (se calcula al leer) y
            // sus contactos abiertos pasan a Administración con el mismo barrido que usa el resto del sistema.
            if (plan.Tipo != AnulacionPagos.TipoSoloPago) await BarridoLeads.BarrerAsync(db, ahora, ct);
            return Results.Ok(await ItemAsync(db, id, ct));
        });

        return app;
    }

    private static EfectoAnulacionResponse Efecto(AnulacionPagos.Plan p) => new(
        p.Pago.Id, p.Tipo, p.SuscripcionId, p.NuevoEstado,
        p.NuevaFin is { } f ? ConsultasCatalogo.Utc(f) : null,
        p.NuevoPlanId is { } id && p.NuevoPlanNombre is { } nombre && p.Tipo != AnulacionPagos.TipoSoloPago ? new PlanEfecto(id, nombre) : null,
        p.QuedaraVencida, p.SuscripcionCanceladaId, p.Bloqueo, p.Mensaje);

    private static async Task<PagoItem> ItemAsync(CamionDbContext db, long id, CancellationToken ct) =>
        (await ItemsAsync(db, db.Pagos.AsNoTracking().Where(p => p.Id == id), ct)).Single();

    // Orden: fecha de pago descendente y luego id descendente. Los nombres se leen aparte (sin datos de contacto).
    private static async Task<List<PagoItem>> ItemsAsync(CamionDbContext db, IQueryable<Pago> consulta, CancellationToken ct)
    {
        var filas = await consulta.ToListAsync(ct);
        var ids = filas.SelectMany(p => new[] { p.RegistradoPor, p.AnuladoPor }).OfType<string>().Distinct().ToList();
        var nombres = ids.Count == 0
            ? new Dictionary<string, string>()
            : await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.NombreCompleto, ct);
        return filas.OrderByDescending(p => p.FechaPago).ThenByDescending(p => p.Id).Select(p => new PagoItem(
            p.Id, p.SuscripcionId, ConsultasCatalogo.Pesos(p.ValorCop), p.FechaPago, p.Medio, p.Referencia, p.Concepto,
            p.Estado,
            p.RegistradoEn is { } r ? ConsultasCatalogo.Utc(r) : null,
            p.RegistradoPor is { } rp ? nombres.GetValueOrDefault(rp) : null,
            p.AnuladoEn is { } a ? ConsultasCatalogo.Utc(a) : null,
            p.AnuladoPor is { } ap ? nombres.GetValueOrDefault(ap) : null,
            p.MotivoAnulacion)).ToList();
    }
}
