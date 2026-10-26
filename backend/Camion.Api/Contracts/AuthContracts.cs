using System.Text.Json.Serialization;

namespace Camion.Api.Contracts;

// La validación es manual en el endpoint (los DataAnnotations no se ejecutan en minimal APIs).
public sealed record RegistroCompradorRequest(
    string? NombreCompleto,
    string? Email,
    string? Telefono,
    string? Password);

// P2 (HU-043, D-3, D-5): registro público de asesor; queda PENDIENTE hasta que el administrador lo apruebe.
public sealed record RegistroAsesorRequest(
    string? NombreCompleto,
    string? Email,
    string? Telefono,
    string? Password,
    string? TipoDocumento,
    string? NumeroDocumento,
    string? NombreComercial);

public sealed record LoginRequest(
    string? Email,
    string? Password);

public sealed record RegistroCompradorResponse(
    string UsuarioId,
    string Mensaje);

public sealed record AuthResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiraEnUtc,
    string UsuarioId,
    string NombreCompleto,
    string Email,
    IReadOnlyList<string> Roles);

public sealed record MeResponse(
    string UsuarioId,
    string NombreCompleto,
    string? Email,
    string? PhoneNumber,
    IReadOnlyList<string> Roles,
    string Estado);

// Formato común de error: { error, errores?, estado? } (errores en 400 y en el 409 de documento;
// estado solo en el 403 del login de una cuenta no ACTIVA).
public sealed record ErrorResponse(
    string Error,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string[]>? Errores = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Estado = null);

public static class MensajesError
{
    public const string Interno = "No se pudo completar la operación. Intenta de nuevo en unos minutos.";
    public const string FormatoInvalido = "La solicitud no tiene un formato válido.";
    public const string SinPermiso = "No tienes permiso para realizar esta acción.";
}
