using Camion.Api.Auth;
using Camion.Api.Data;
using Camion.Api.Endpoints;
using Camion.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace Camion.Api.Comandos;

public sealed record ResultadoRecuperarAdmin(int CodigoSalida, string Mensaje, string? Enlace = null, DateTime? VenceEn = null);

// D-79 (opción A), D-73 D, D-78: comando local `recuperar-admin <correo>` en el equipo del servidor, como promover-admin
// (D-13). Genera el enlace de un solo uso (60 minutos) para una cuenta de ADMINISTRADOR activa, con el mismo token de
// Identity y DataProtection que el panel pero con otro propósito, para que los enlaces no se puedan intercambiar.
// El enlace solo se devuelve al llamador para imprimirlo en la consola; nunca va a un log ni a la auditoría.
public static class RecuperarAdmin
{
    public const string NombreComando = "recuperar-admin";
    public const string AccionAuditoria = "GENERAR_ENLACE_RECUPERACION_ADMIN";
    public const string ActorComandoLocal = "COMANDO_LOCAL";
    // Mensaje genérico para cualquier cuenta que no sea un administrador activo (no revela si el correo existe).
    public const string MensajeNoAplica = "No se generó ningún enlace: el correo no corresponde a una cuenta de administrador activa.";
    public const string MensajeError = "No se pudo generar el enlace. Intenta de nuevo.";

    public static async Task<ResultadoRecuperarAdmin> EjecutarAsync(string? correo, UserManager<ApplicationUser> users,
        CamionDbContext db, TimeProvider reloj, string urlPortal)
    {
        var email = ValidacionAuth.NormalizarEmail(correo);
        if (email.Length == 0) return new(2, $"Uso: {NombreComando} <correo>");

        var user = await users.FindByEmailAsync(email);
        if (user is null || user.EstadoCuenta != EstadosCuenta.Activa || !await users.IsInRoleAsync(user, Roles.Administrador))
            return new(1, MensajeNoAplica);

        // Igual que el panel: cambiar el SecurityStamp invalida cualquier enlace anterior de la cuenta.
        if (!(await users.UpdateSecurityStampAsync(user)).Succeeded) return new(1, MensajeError);
        var codigo = await users.GenerateUserTokenAsync(user, ProveedorTokenRecuperacion.Nombre,
            ProveedorTokenRecuperacion.PropositoAdministrador);
        var ahora = reloj.GetUtcNow().UtcDateTime;
        var vence = DateTime.SpecifyKind(ahora + ProveedorTokenRecuperacion.Vigencia, DateTimeKind.Utc);
        Auditoria.AgregarEvento(db, null, Auditoria.EntidadUsuario, user.Id, AccionAuditoria, null,
            new Dictionary<string, object?> { ["origen"] = ActorComandoLocal, ["venceEn"] = vence }, ahora);
        await db.SaveChangesAsync();

        var enlace = urlPortal.Trim().TrimEnd('/') + RecuperacionEndpoints.RutaFrontend + codigo;
        return new(0,
            $"Enlace de un solo uso para {email}. Vence en 60 minutos ({vence:yyyy-MM-dd HH:mm} UTC). " +
            "Ábrelo en el navegador y define la contraseña nueva; no lo compartas ni lo guardes en documentos.",
            enlace, vence);
    }
}
