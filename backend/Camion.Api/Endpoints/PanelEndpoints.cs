using Camion.Api.Auth;
using Camion.Api.Catalogo;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Leads;
using Camion.Api.Models;
using Camion.Api.Publicaciones;
using Camion.Api.Suscripciones;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Endpoints;

// P7 (HU-075 parcial, HU-073/HU-074 parcial, RN-RE-001, D-64 A): contadores operativos del panel del administrador.
// Solo números: ningún nombre, teléfono, correo ni suma de dinero (CA-P7-48, CA-P7-51).
public static class PanelEndpoints
{
    // Orden fijo de las solicitudes en la respuesta.
    private static readonly string[] OrigenesPanel =
    [
        Lead.OrigenContactoVehiculo, Lead.OrigenVendeTuCamion, Lead.OrigenSeguroTodoRiesgo, Lead.OrigenCredito, Lead.OrigenBusqueda
    ];

    public const int DiasVentas = 30;

    public static IEndpointRouteBuilder MapPanelEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/panel", async (CamionDbContext db, TimeProvider reloj, CancellationToken ct) =>
        {
            var ahora = reloj.GetUtcNow().UtcDateTime;
            // Igual que GET /api/leads/resumen: el barrido de D-46 antes de contar.
            await BarridoLeads.BarrerAsync(db, ahora, ct);

            var asesorRolId = await db.Roles.AsNoTracking().Where(r => r.NormalizedName == Roles.Asesor)
                .Select(r => r.Id).FirstOrDefaultAsync(ct);
            var pendientes = asesorRolId is null
                ? 0
                : await db.Users.AsNoTracking().CountAsync(u => u.EstadoCuenta == EstadosCuenta.Pendiente &&
                    db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == asesorRolId), ct);

            // Solicitudes abiertas a cargo de Administración (sin asesor responsable) y cuántas no se han visto.
            var leads = await db.Leads.AsNoTracking()
                .Where(l => l.Estado == Lead.Abierto && l.ResponsableAsesorId == null)
                .GroupBy(l => l.Origen)
                .Select(g => new { Origen = g.Key, Abiertas = g.Count(), Nuevas = g.Count(l => !l.VistoPorResponsable) })
                .ToListAsync(ct);
            var solicitudes = OrigenesPanel.Select(o =>
            {
                var g = leads.FirstOrDefault(x => x.Origen == o);
                return new SolicitudesPorOrigen(o, g?.Abiertas ?? 0, g?.Nuevas ?? 0);
            }).ToArray();

            var vigentes = DatosP4b.SuscripcionesVigentes(db, ahora);
            var limite = ahora.AddDays(ReglasP4p.DiasAvisoVencimiento);
            var porVencer = await vigentes.CountAsync(s => s.FinVigencia <= limite, ct);
            // Vencidas (estado efectivo VENCIDA) cuyo asesor no tiene hoy una suscripción vigente.
            var vencidas = await db.Suscripciones.AsNoTracking().CountAsync(s =>
                (s.Estado == Suscripcion.Vencida ||
                 ((s.Estado == Suscripcion.Activa || s.Estado == Suscripcion.Reactivada) &&
                  s.FinVigencia != null && s.FinVigencia <= ahora)) &&
                !vigentes.Any(v => v.AsesorId == s.AsesorId), ct);

            var visibles = await ConsultasCatalogo.Visibles(db, ahora).CountAsync(ct);

            var porEstado = await db.Vehiculos.AsNoTracking().GroupBy(v => v.Estado)
                .Select(g => new { Estado = g.Key, Total = g.Count() }).ToListAsync(ct);
            var vehiculos = Vehiculo.Estados.ToDictionary(e => e, e => porEstado.FirstOrDefault(x => x.Estado == e)?.Total ?? 0);

            // 30 días contando hoy (fecha de Colombia): igual que GET /api/ventas?estado=VIGENTE&desde={ventasDesde}.
            var desde = ReglasP4b.HoyColombia(ahora).AddDays(-(DiasVentas - 1));
            var ventas = await db.VentasVehiculo.AsNoTracking()
                .CountAsync(v => v.RevertidaEn == null && v.FechaVenta >= desde, ct);

            return Results.Ok(new PanelAdminResponse(pendientes, solicitudes, porVencer, vencidas, visibles, vehiculos,
                ventas, desde));
        }).RequireAuthorization(Roles.PoliticaSoloAdministrador);

        return app;
    }
}
