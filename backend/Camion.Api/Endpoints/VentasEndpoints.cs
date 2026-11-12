using System.Globalization;
using System.Security.Claims;
using Camion.Api.Auth;
using Camion.Api.Catalogo;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Inventario;
using Camion.Api.Models;
using Camion.Api.Publicaciones;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Endpoints;

// P4b: marcar vendido (HU-064, RN-GC-001, RN-VE-014, D-22, D-39) y revertir la venta (minuta), con el mismo alcance que el
// inventario (dueño o administrador; lo ajeno da 404). Reasignar un vehículo a otro asesor (D-34): solo el administrador.
// Las tres responden con el detalle del vehículo del inventario (DetalleInventario), como las demás acciones de P4a.
public static class VentasEndpoints
{
    public static IEndpointRouteBuilder MapVentasEndpoints(this IEndpointRouteBuilder app)
    {
        var inventario = app.MapGroup("/api/inventario").RequireAuthorization(Roles.PoliticaAsesorOAdministrador);

        // HU-064: ACTIVO o RESERVADO -> VENDIDO; la publicación pasa a VENDIDO (sale del portal) y pierde su destacado.
        inventario.MapPost("/vehiculos/{id}/vender", async (long id, VentaRequest? request, ClaimsPrincipal principal,
            CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var actor = await InventarioEndpoints.ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();
            var previo = await InventarioEndpoints.Alcance(db.Vehiculos.AsNoTracking(), actor).Where(v => v.Id == id)
                .Select(v => new { v.CreadoEn }).FirstOrDefaultAsync(ct);
            if (previo is null) return NoEncontrado();

            var ahora = reloj.GetUtcNow().UtcDateTime;
            var errores = ReglasP4b.ValidarVenta(request, ahora, previo.CreadoEn, out var datos);
            if (errores.Count > 0 || datos is null)
                return Results.BadRequest(new ErrorResponse(ReglasP4b.MensajeRevisar, errores));

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await DatosP4b.BloquearVehiculoAsync(db, id, ct);
            var vehiculo = await db.Vehiculos.SingleAsync(v => v.Id == id, ct);
            if (!ReglasP4b.VehiculoPublicable(vehiculo.Estado))
                return Results.Conflict(new ErrorResponse(ReglasP4b.MensajeVehiculoTransicion("vender", vehiculo.Estado)));
            var pub = await PublicacionDe(db, id, ct);

            var antes = new Dictionary<string, object?> { ["estadoVehiculo"] = vehiculo.Estado };
            if (pub is not null) antes["estadoComercial"] = pub.EstadoComercial;
            vehiculo.Estado = Vehiculo.Vendido;
            vehiculo.NotaReserva = null;   // D-28: la nota de reserva se borra al vender.
            vehiculo.ActualizadoEn = ahora;
            var quitados = 0;
            if (pub is not null)
            {
                pub.EstadoComercial = Publicacion.Vendido;
                pub.ActualizadoEn = ahora;
                quitados = await DatosP4b.QuitarDestacadosAsync(db, pub.Id, ahora, ct);
            }
            var venta = new VentaVehiculo
            {
                VehiculoId = id,
                PublicacionId = pub?.Id,
                FechaVenta = datos.Fecha,
                PrecioFinalCop = datos.PrecioFinalCop,
                Origen = datos.Origen,
                Observaciones = datos.Observaciones,
                RegistradaPor = actor.UsuarioId,
                RegistradaEn = ahora
            };
            db.VentasVehiculo.Add(venta);
            await db.SaveChangesAsync(ct);

            var nuevos = new Dictionary<string, object?>
            {
                ["vehiculoId"] = id,
                ["publicacionId"] = pub?.Id,
                ["estadoVehiculo"] = vehiculo.Estado,
                ["fechaVenta"] = datos.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["precioFinalCop"] = ConsultasCatalogo.Pesos(datos.PrecioFinalCop),
                ["origen"] = datos.Origen,
                ["observaciones"] = datos.Observaciones
            };
            if (pub is not null) nuevos["estadoComercial"] = pub.EstadoComercial;
            if (quitados > 0) nuevos["destacadoQuitado"] = true;
            Auditoria.AgregarEvento(db, actor.UsuarioId, Auditoria.EntidadVenta, Id(venta.Id), Auditoria.Vender, antes, nuevos, ahora);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(await InventarioEndpoints.DetalleAsync(db, actor, id, ahora, ct));
        });

        // Minuta: VENDIDO -> ACTIVO; la publicación vuelve a DISPONIBLE pero queda OCULTA (se reactiva con plan).
        inventario.MapPost("/vehiculos/{id}/revertir-venta", async (long id, MotivoPublicacionRequest? request,
            ClaimsPrincipal principal, CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var actor = await InventarioEndpoints.ActorAsync(principal, db, ct);
            if (actor is null) return SinPermiso();
            if (!await InventarioEndpoints.Alcance(db.Vehiculos.AsNoTracking(), actor).AnyAsync(v => v.Id == id, ct))
                return NoEncontrado();
            var errores = ValidacionAuth.ValidarMotivo(request?.Motivo, obligatorio: true);
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasP4b.MensajeRevisar, errores));
            var motivo = ValidacionAuth.NormalizarOpcional(request!.Motivo)!;

            var ahora = reloj.GetUtcNow().UtcDateTime;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await DatosP4b.BloquearVehiculoAsync(db, id, ct);
            var vehiculo = await db.Vehiculos.SingleAsync(v => v.Id == id, ct);
            if (vehiculo.Estado != Vehiculo.Vendido)
                return Results.Conflict(new ErrorResponse(ReglasP4b.MensajeVehiculoTransicion("revertir la venta de", vehiculo.Estado)));
            var venta = await db.VentasVehiculo.Where(v => v.VehiculoId == id && v.RevertidaEn == null)
                .OrderByDescending(v => v.RegistradaEn).ThenByDescending(v => v.Id).FirstOrDefaultAsync(ct);
            var pub = await PublicacionDe(db, id, ct);

            var antes = new Dictionary<string, object?> { ["estadoVehiculo"] = vehiculo.Estado };
            if (pub is not null)
            {
                antes["estadoComercial"] = pub.EstadoComercial;
                antes["estadoPublicacion"] = pub.EstadoPublicacion;
            }
            vehiculo.Estado = Vehiculo.Activo;
            vehiculo.ActualizadoEn = ahora;
            if (pub is not null)
            {
                pub.EstadoComercial = Publicacion.Disponible;
                if (pub.EstadoPublicacion == Publicacion.Publicada)
                {
                    pub.EstadoPublicacion = Publicacion.Oculta;
                    pub.MotivoOcultamiento = motivo;
                    pub.OcultadaEn = ahora;
                }
                pub.ActualizadoEn = ahora;
            }
            if (venta is not null)
            {
                venta.RevertidaPor = actor.UsuarioId;
                venta.RevertidaEn = ahora;
                venta.MotivoReversion = motivo;
            }
            var nuevos = new Dictionary<string, object?>
            {
                ["vehiculoId"] = id,
                ["publicacionId"] = pub?.Id,
                ["estadoVehiculo"] = vehiculo.Estado,
                ["motivo"] = motivo
            };
            if (pub is not null)
            {
                nuevos["estadoComercial"] = pub.EstadoComercial;
                nuevos["estadoPublicacion"] = pub.EstadoPublicacion;
            }
            // Un vehículo VENDIDO sin fila de venta (DEMO o anterior a P4b) se audita sobre el vehículo.
            Auditoria.AgregarEvento(db, actor.UsuarioId, venta is null ? Auditoria.EntidadVehiculo : Auditoria.EntidadVenta,
                venta is null ? Id(id) : Id(venta.Id), Auditoria.RevertirVenta, antes, nuevos, ahora);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(await InventarioEndpoints.DetalleAsync(db, actor, id, ahora, ct));
        });

        // D-34: solo el administrador, con motivo. El destino debe ser un asesor ACTIVA con cupo si la publicación cuenta.
        app.MapPost("/api/admin/vehiculos/{id}/reasignar", async (long id, ReasignarRequest? request, ClaimsPrincipal principal,
            CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var actor = await InventarioEndpoints.ActorAsync(principal, db, ct);
            if (actor is null || !actor.EsAdmin) return SinPermiso();
            if (!await db.Vehiculos.AsNoTracking().AnyAsync(v => v.Id == id, ct)) return NoEncontrado();
            var errores = ValidacionAuth.ValidarMotivo(request?.Motivo, obligatorio: true);
            var destino = request?.AsesorId ?? 0;
            if (request?.AsesorId is null || !await InventarioEndpoints.AsesorActivoAsync(db, destino, ct))
                errores["asesorId"] = [ReglasInventario.MensajeAsesor];
            if (errores.Count > 0) return Results.BadRequest(new ErrorResponse(ReglasP4b.MensajeRevisar, errores));
            var motivo = ValidacionAuth.NormalizarOpcional(request!.Motivo)!;

            var ahora = reloj.GetUtcNow().UtcDateTime;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await DatosP4b.BloquearAsesorAsync(db, destino, ct);
            await DatosP4b.BloquearVehiculoAsync(db, id, ct);
            var vehiculo = await db.Vehiculos.SingleAsync(v => v.Id == id, ct);
            if (vehiculo.AsesorId == destino) return Results.Conflict(new ErrorResponse(ReglasP4b.MensajeMismoAsesor));
            var pub = await PublicacionDe(db, id, ct);
            Suscripcion? sus = null;
            if (pub is not null)
            {
                // D-40: la publicación no eliminada cuenta en el cupo del destino.
                sus = await DatosP4b.SuscripcionVigenteAsync(db, destino, ahora, ct);
                if (sus is null) return Results.Conflict(new ErrorResponse(ReglasP4b.MensajeDestinoSinPlan));
                var limite = sus.Plan!.LimitePublicaciones;
                if (await DatosP4b.CupoUsadoAsync(db, destino, ct) >= limite)
                    return Results.Conflict(new ErrorResponse(ReglasP4b.MensajeDestinoSinCupo(limite)));
            }

            var anterior = vehiculo.AsesorId;
            vehiculo.AsesorId = destino;
            vehiculo.ActualizadoEn = ahora;
            var quitados = 0;
            if (pub is not null)
            {
                pub.AsesorId = destino;
                pub.ActualizadoEn = ahora;
                quitados = await DatosP4b.QuitarDestacadosAsync(db, pub.Id, ahora, ct);
                // Historial: si sigue publicada, su vigencia continúa con el plan del nuevo dueño.
                if (pub.EstadoPublicacion == Publicacion.Publicada && pub.EstadoComercial != Publicacion.Vendido)
                    db.HistorialVigencias.Add(PublicacionesEndpoints.NuevaVigencia(pub.Id, sus!, ahora, ReglasP4b.MotivoReasignar));
            }
            var nuevos = new Dictionary<string, object?>
            {
                ["asesorId"] = destino,
                ["publicacionId"] = pub?.Id,
                ["motivo"] = motivo
            };
            if (quitados > 0) nuevos["destacadoQuitado"] = true;
            Auditoria.AgregarVehiculo(db, actor.UsuarioId, id, Auditoria.Reasignar,
                new Dictionary<string, object?> { ["asesorId"] = anterior }, nuevos, ahora);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(await InventarioEndpoints.DetalleAsync(db, actor, id, ahora, ct));
        }).RequireAuthorization(Roles.PoliticaSoloAdministrador);

        return app;
    }

    // La publicación no eliminada del vehículo (como máximo una), con seguimiento.
    private static Task<Publicacion?> PublicacionDe(CamionDbContext db, long vehiculoId, CancellationToken ct) =>
        db.Publicaciones.Where(p => p.VehiculoId == vehiculoId && p.EstadoPublicacion != Publicacion.Eliminada)
            .OrderByDescending(p => p.Id).FirstOrDefaultAsync(ct);

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static IResult NoEncontrado() => Results.NotFound(new ErrorResponse(ReglasP4b.MensajeVehiculoNoEncontrado));

    private static IResult SinPermiso() =>
        Results.Json(new ErrorResponse(MensajesError.SinPermiso), statusCode: StatusCodes.Status403Forbidden);
}
