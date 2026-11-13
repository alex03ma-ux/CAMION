using System.Text;
using Camion.Api.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Camion.Api.Auth;

/// <summary>P8 (D-73 D): configuración de la recuperación asistida (sección "Recuperacion"). No contiene secretos.</summary>
public sealed class OpcionesRecuperacion
{
    // Límite por IP de POST /api/auth/restablecer-contrasena.
    public int LimiteVentana { get; set; } = 5;
    public int VentanaMinutos { get; set; } = 15;
    // Base del enlace que el administrador copia y envía por WhatsApp (el frontend).
    public string UrlPortal { get; set; } = "http://localhost:5173";
}

/// <summary>
/// Marca la ruta pública de restablecer la contraseña: el limitador global de Program.cs le aplica su propio cupo por IP,
/// separado del de contactos y avisos (<see cref="Camion.Api.Leads.LimitePeticionesContacto"/>).
/// </summary>
public sealed class LimitePeticionesRecuperacion;

/// <summary>
/// P8 (HU-063, RN-US-006, D-73 D): token de restablecimiento de contraseña de un solo uso que vence en 60 minutos.
/// Es un token de Identity protegido con DataProtection (cifrado y firmado): lleva la hora de emisión (del
/// <see cref="TimeProvider"/> de la API), el usuario, el propósito y el SecurityStamp. No se guarda en ninguna tabla:
/// al restablecer la contraseña Identity cambia el SecurityStamp y el token deja de valer; el endpoint de generación también
/// lo cambia antes de emitir uno nuevo, así que solo vale el último enlace. Nunca se escribe en logs ni en la auditoría.
/// </summary>
public sealed class ProveedorTokenRecuperacion : IUserTwoFactorTokenProvider<ApplicationUser>
{
    public const string Nombre = "RecuperacionAsistida";
    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(60);
    private const string Proposito = "Camion.Api.RecuperacionAsistida.v1";

    private readonly IDataProtector _protector;
    private readonly TimeProvider _reloj;

    public ProveedorTokenRecuperacion(IDataProtectionProvider proteccion, TimeProvider reloj)
    {
        _protector = proteccion.CreateProtector(Proposito);
        _reloj = reloj;
    }

    public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<ApplicationUser> manager, ApplicationUser user) =>
        Task.FromResult(false);

    public async Task<string> GenerateAsync(string purpose, UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        var sello = await manager.GetSecurityStampAsync(user);
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(_reloj.GetUtcNow().UtcTicks);
            w.Write(user.Id);
            w.Write(purpose);
            w.Write(sello ?? "");
        }
        return WebEncoders.Base64UrlEncode(_protector.Protect(ms.ToArray()));
    }

    public async Task<bool> ValidateAsync(string purpose, string token, UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        var datos = Leer(token);
        if (datos is null) return false;
        var (emitido, usuario, proposito, sello) = datos.Value;
        var ahora = _reloj.GetUtcNow();
        if (emitido > ahora.AddMinutes(1) || emitido + Vigencia <= ahora) return false;
        if (usuario != user.Id || proposito != purpose) return false;
        return sello == (await manager.GetSecurityStampAsync(user) ?? "");
    }

    /// <summary>Usuario del código, o null si el código no es válido (mal formado, alterado o de otra clave).</summary>
    public string? UsuarioDe(string? token) => Leer(token)?.Usuario;

    private (DateTimeOffset Emitido, string Usuario, string Proposito, string Sello)? Leer(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) return null;
        try
        {
            var bytes = _protector.Unprotect(WebEncoders.Base64UrlDecode(token.Trim()));
            using var r = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8);
            var ticks = r.ReadInt64();
            return (new DateTimeOffset(ticks, TimeSpan.Zero), r.ReadString(), r.ReadString(), r.ReadString());
        }
        catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException
                                       or EndOfStreamException or IOException or ArgumentException)
        {
            return null;
        }
    }
}
