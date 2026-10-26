using System.Text.Json.Serialization;

namespace Camion.Api.Contracts;

// La validación es manual en el endpoint (los DataAnnotations no se ejecutan en minimal APIs).
public sealed record RegistroCompradorRequest(
    string? NombreCompleto,
    string? Email,
    string? Telefono,
    string? Password);

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
    IReadOnlyList<string> Roles);

// Formato común de error: { error, errores } (errores solo en 400).
public sealed record ErrorResponse(
    string Error,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string[]>? Errores = null);
