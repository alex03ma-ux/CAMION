using System.Security.Claims;
using Camion.Api.Auth;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Leads;
using Camion.Api.Models;
using Camion.Api.Parametros;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Camion.Api.Endpoints;

// P7 (HU-081, RN-PA-001, D-63 A): el administrador edita los datos de contacto del portal (WhatsApp, correo, horario,
// Instagram y TikTok). El pie del portal los lee de una ruta pública de solo lectura, fuera de /api/catalogo (CA-P7-42).
public static class ParametrosEndpoints
{
    public static IEndpointRouteBuilder MapParametrosEndpoints(this IEndpointRouteBuilder app)
    {
        // Pública: solo los datos de contacto (CA-P7-42).
        app.MapGet("/api/portal/contacto", async (CamionDbContext db, IOptions<OpcionesContacto> opciones,
            CancellationToken ct) =>
            Results.Ok(ParametrosPortal.Publico((await ParametrosPortal.LeerAsync(db, opciones.Value.WhatsappPortal, ct)).Valor)))
            .AllowAnonymous();

        var admin = app.MapGroup("/api/admin/parametros").RequireAuthorization(Roles.PoliticaSoloAdministrador);

        admin.MapGet("/contacto", async (CamionDbContext db, IOptions<OpcionesContacto> opciones, CancellationToken ct) =>
            Results.Ok(await AdminAsync(db, await ParametrosPortal.LeerAsync(db, opciones.Value.WhatsappPortal, ct), ct)));

        // CA-P7-36, CA-P7-37: valida todo antes de escribir; valor y auditoría en la misma transacción.
        admin.MapPut("/contacto", async (ContactoPortalRequest? request, ClaimsPrincipal principal, CamionDbContext db,
            IOptions<OpcionesContacto> opciones, TimeProvider reloj, CancellationToken ct) =>
        {
            var errores = ParametrosPortal.Validar(request, out var valor);
            if (errores.Count > 0 || valor is null)
                return Results.BadRequest(new ErrorResponse(MensajesP7.Revisar, errores));

            var adminId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var ahora = reloj.GetUtcNow().UtcDateTime;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var anterior = await ParametrosPortal.LeerAsync(db, opciones.Value.WhatsappPortal, ct);
            if (!anterior.Configurado || !ParametrosPortal.Iguales(anterior.Valor, valor))
            {
                var fila = await db.ConfiguracionesSistema.FirstOrDefaultAsync(c => c.Clave == ParametrosPortal.ClaveContacto, ct);
                if (fila is null)
                {
                    fila = new ConfiguracionSistema { Clave = ParametrosPortal.ClaveContacto };
                    db.ConfiguracionesSistema.Add(fila);
                }
                fila.ValorJson = ParametrosPortal.Serializar(valor);
                fila.ActualizadoPor = adminId;
                fila.ActualizadoEn = ahora;
                Auditoria.AgregarEvento(db, adminId, Auditoria.EntidadParametro, ParametrosPortal.ClaveContacto,
                    Auditoria.Cambiar, anterior.Configurado ? ParametrosPortal.Datos(anterior.Valor) : null,
                    ParametrosPortal.Datos(valor), ahora);
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return Results.Ok(await AdminAsync(db, await ParametrosPortal.LeerAsync(db, opciones.Value.WhatsappPortal, ct), ct));
        });

        return app;
    }

    private static async Task<ContactoPortalAdmin> AdminAsync(CamionDbContext db, ParametrosPortal.Leido l, CancellationToken ct)
    {
        var nombre = l.ActualizadoPor is null
            ? null
            : await db.Users.AsNoTracking().Where(u => u.Id == l.ActualizadoPor).Select(u => u.NombreCompleto).FirstOrDefaultAsync(ct);
        var c = l.Valor;
        return new ContactoPortalAdmin(c.Whatsapp, ParametrosPortal.Enlace(c), c.Correo, c.Horario, c.Instagram, c.Tiktok,
            l.Configurado, l.ActualizadoEn, nombre);
    }
}

// P7: mensajes comunes de las rutas nuevas.
public static class MensajesP7
{
    public const string Revisar = "Revisa los datos marcados.";
    public const string Filtros = "Revisa los filtros.";
}
