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
        // Pública: solo los datos de contacto (CA-P7-42) y, desde P9, los de la empresa si están completos (CA-P9-33).
        app.MapGet("/api/portal/contacto", async (CamionDbContext db, IOptions<OpcionesContacto> opciones,
            CancellationToken ct) =>
        {
            var contacto = ParametrosPortal.Publico((await ParametrosPortal.LeerAsync(db, opciones.Value.WhatsappPortal, ct)).Valor);
            var empresa = ParametrosEmpresa.Publica((await ParametrosEmpresa.LeerAsync(db, ct)).Valor);
            return Results.Ok(contacto with { Empresa = empresa });
        })
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

        // P9 (HU-085, RN-PA-005, D-63 B; CA-P9-25): datos de la empresa, sin valores por defecto.
        admin.MapGet("/empresa", async (CamionDbContext db, CancellationToken ct) =>
            Results.Ok(await EmpresaAdminAsync(db, await ParametrosEmpresa.LeerAsync(db, ct), ct)));

        // CA-P9-26 a CA-P9-31: valida todo antes de escribir; valor y auditoría en la misma transacción; sin cambios no se
        // escribe ni se audita.
        admin.MapPut("/empresa", async (EmpresaPortalRequest? request, ClaimsPrincipal principal, CamionDbContext db,
            TimeProvider reloj, CancellationToken ct) =>
        {
            // Cuerpo vacío: formato común (CA-P9-31). JSON no válido o con tipos incorrectos lo atiende ThrowOnBadRequest.
            if (request is null) return Results.BadRequest(new ErrorResponse(MensajesError.FormatoInvalido));
            var errores = ParametrosEmpresa.Validar(request, out var valor);
            if (errores.Count > 0 || valor is null)
                return Results.BadRequest(new ErrorResponse(MensajesP7.Revisar, errores));

            var adminId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var ahora = reloj.GetUtcNow().UtcDateTime;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var anterior = await ParametrosEmpresa.LeerAsync(db, ct);
            // Sin fila guardada equivale a los tres en null: guardar los tres vacíos no escribe nada.
            if (!ParametrosEmpresa.Iguales(anterior.Valor, valor))
            {
                var fila = await db.ConfiguracionesSistema.FirstOrDefaultAsync(c => c.Clave == ParametrosEmpresa.Clave, ct);
                if (fila is null)
                {
                    fila = new ConfiguracionSistema { Clave = ParametrosEmpresa.Clave };
                    db.ConfiguracionesSistema.Add(fila);
                }
                fila.ValorJson = ParametrosEmpresa.Serializar(valor);
                fila.ActualizadoPor = adminId;
                fila.ActualizadoEn = ahora;
                Auditoria.AgregarEvento(db, adminId, Auditoria.EntidadParametro, ParametrosEmpresa.Clave,
                    Auditoria.Cambiar, anterior.Configurado ? ParametrosEmpresa.Datos(anterior.Valor) : null,
                    ParametrosEmpresa.Datos(valor), ahora);
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return Results.Ok(await EmpresaAdminAsync(db, await ParametrosEmpresa.LeerAsync(db, ct), ct));
        });

        return app;
    }

    private static async Task<EmpresaPortalAdmin> EmpresaAdminAsync(CamionDbContext db, ParametrosEmpresa.Leido l,
        CancellationToken ct)
    {
        var e = l.Valor;
        return new EmpresaPortalAdmin(e.RazonSocial, e.Nit, e.Direccion, ParametrosEmpresa.Completa(e), l.Configurado,
            l.ActualizadoEn, await NombreAsync(db, l.ActualizadoPor, ct));
    }

    private static async Task<string?> NombreAsync(CamionDbContext db, string? usuarioId, CancellationToken ct) =>
        usuarioId is null
            ? null
            : await db.Users.AsNoTracking().Where(u => u.Id == usuarioId).Select(u => u.NombreCompleto).FirstOrDefaultAsync(ct);

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
