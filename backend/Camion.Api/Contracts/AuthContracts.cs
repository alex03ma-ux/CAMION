using System.ComponentModel.DataAnnotations;

namespace Camion.Api.Contracts;

public sealed record RegistroCompradorRequest(
    [Required, MinLength(2), MaxLength(150)] string NombreCompleto,
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MaxLength(25)] string Telefono,
    [Required, MinLength(8)] string Password);

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record AuthResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiraEnUtc,
    string UsuarioId,
    string NombreCompleto,
    string Email,
    IReadOnlyList<string> Roles);
