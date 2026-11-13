using System.Security.Claims;
using Camion.Api.Auth;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Camion.Api.Endpoints;

// P8 (HU-063, RN-US-006, D-73 D, D-12): recuperación asistida de contraseña, sin correo ni servicios externos. El
// administrador genera un enlace de un solo uso (60 minutos) y lo envía por WhatsApp; el usuario define una contraseña nueva
// con las reglas de P1 y se cierran todas sus sesiones. Nunca se escriben el enlace, el código ni la contraseña en logs ni
// en la auditoría.
public static class RecuperacionEndpoints
{
    public const string MensajeNoEncontrado = "Usuario no encontrado.";
    public const string MensajePropia = "No puedes generar un enlace para tu propia cuenta.";
    public const string MensajeCuentaAdministrador = "No se puede modificar una cuenta de administrador.";
    public const string MensajeNoActiva = "Solo se puede generar un enlace para una cuenta activa.";
    public const string MensajeEnlace =
        "Envía este enlace por WhatsApp a la persona después de confirmar su identidad. Sirve una sola vez y vence en 60 minutos.";
    public const string MensajeCodigo = "El enlace no es válido o ya venció. Pide uno nuevo al administrador.";
    public const string MensajeRevisar = "Revisa los datos marcados.";
    public const string MensajeListo = "Listo. Tu contraseña cambió; inicia sesión con la nueva.";
    public const string MensajeMuchos = "Demasiados intentos. Espera unos minutos e intenta de nuevo.";
    public const string RutaFrontend = "/#/restablecer-contrasena?codigo=";

    public static IEndpointRouteBuilder MapRecuperacionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/admin/usuarios/{id}/enlace-recuperacion", async (string id, HttpContext http, ClaimsPrincipal principal,
            UserManager<ApplicationUser> users, CamionDbContext db, TimeProvider reloj, IOptions<OpcionesRecuperacion> opciones,
            ILoggerFactory loggerFactory) =>
        {
            var adminId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (adminId is null) return Results.Unauthorized();
            var user = await users.FindByIdAsync(id);
            if (user is null) return Results.NotFound(new ErrorResponse(MensajeNoEncontrado));
            if (user.Id == adminId) return Prohibido(MensajePropia);
            if (await users.IsInRoleAsync(user, Roles.Administrador)) return Prohibido(MensajeCuentaAdministrador);
            if (user.EstadoCuenta != EstadosCuenta.Activa) return Results.Conflict(new ErrorResponse(MensajeNoActiva));

            // Cambiar el SecurityStamp invalida cualquier enlace anterior: solo vale el último.
            if (!(await users.UpdateSecurityStampAsync(user)).Succeeded) return ErrorInterno();
            var codigo = await users.GeneratePasswordResetTokenAsync(user);
            var ahora = reloj.GetUtcNow().UtcDateTime;
            var vence = DateTime.SpecifyKind(ahora + ProveedorTokenRecuperacion.Vigencia, DateTimeKind.Utc);
            Auditoria.AgregarEvento(db, adminId, Auditoria.EntidadUsuario, user.Id, Auditoria.GenerarEnlaceRecuperacion, null,
                new Dictionary<string, object?> { ["venceEn"] = vence }, ahora);
            await db.SaveChangesAsync();
            loggerFactory.CreateLogger(AuthEndpoints.CategoriaAuditoria).LogInformation(
                "Auditoria enlace_recuperacion usuarioId={UsuarioId} administradorId={AdministradorId}", user.Id, adminId);

            http.Response.Headers.CacheControl = "no-store";
            var enlace = opciones.Value.UrlPortal.TrimEnd('/') + RutaFrontend + codigo;
            return Results.Ok(new EnlaceRecuperacionResponse(enlace, vence, MensajeEnlace));
        }).RequireAuthorization(Roles.PoliticaSoloAdministrador);

        app.MapPost("/api/auth/restablecer-contrasena", async (RestablecerContrasenaRequest? request,
            UserManager<ApplicationUser> users, ProveedorTokenRecuperacion proveedor, CamionDbContext db, TimeProvider reloj,
            ILoggerFactory loggerFactory) =>
        {
            var errores = new Dictionary<string, string[]>();
            var password = request?.Password ?? "";
            var erroresPassword = ValidacionAuth.ErroresPassword(password);
            if (erroresPassword.Count > 0) errores["password"] = erroresPassword.ToArray();
            var codigo = (request?.Codigo ?? "").Trim();
            var usuarioId = proveedor.UsuarioDe(codigo);
            if (usuarioId is null) errores["codigo"] = [MensajeCodigo];
            if (errores.Count > 0) return Validacion(errores);

            var user = await users.FindByIdAsync(usuarioId!);
            if (user is null || user.EstadoCuenta != EstadosCuenta.Activa) return CodigoNoValido();

            await using var tx = await db.Database.BeginTransactionAsync();
            var reset = await users.ResetPasswordAsync(user, codigo, password);
            if (!reset.Succeeded)
            {
                await tx.RollbackAsync();
                if (reset.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))) return CodigoNoValido();
                var traducidos = ValidacionAuth.TraducirErroresIdentity(reset.Errors);
                return traducidos.Count > 0 ? Validacion(traducidos) : CodigoNoValido();
            }
            // D-12: cierra la sesión en todos los equipos; HU-062: quita el bloqueo temporal por intentos fallidos.
            user.SessionVersion++;
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
            if (!(await users.UpdateAsync(user)).Succeeded)
            {
                await tx.RollbackAsync();
                return ErrorInterno();
            }
            var ahora = reloj.GetUtcNow().UtcDateTime;
            Auditoria.AgregarEvento(db, user.Id, Auditoria.EntidadUsuario, user.Id, Auditoria.RestablecerContrasena, null,
                new Dictionary<string, object?> { ["sesionesCerradas"] = true }, ahora);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            loggerFactory.CreateLogger(AuthEndpoints.CategoriaAuditoria).LogInformation(
                "Auditoria restablecer_contrasena usuarioId={UsuarioId}", user.Id);
            return Results.Ok(new MensajeResponse(MensajeListo));
        }).AllowAnonymous().WithMetadata(new LimitePeticionesRecuperacion());

        return app;
    }

    private static IResult Validacion(Dictionary<string, string[]> errores) =>
        Results.BadRequest(new ErrorResponse(MensajeRevisar, errores));

    private static IResult CodigoNoValido() =>
        Validacion(new Dictionary<string, string[]> { ["codigo"] = [MensajeCodigo] });

    private static IResult Prohibido(string mensaje) =>
        Results.Json(new ErrorResponse(mensaje), statusCode: StatusCodes.Status403Forbidden);

    private static IResult ErrorInterno() =>
        Results.Json(new ErrorResponse(MensajesError.Interno), statusCode: StatusCodes.Status500InternalServerError);
}
