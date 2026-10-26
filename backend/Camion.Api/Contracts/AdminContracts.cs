namespace Camion.Api.Contracts;

// P2 (HU-043 a HU-048): administración de usuarios. La respuesta nunca incluye hash, SecurityStamp
// ni otros campos internos de Identity.
public sealed record AccionUsuarioRequest(string? Motivo);

public sealed record AsesorResumen(
    string TipoDocumento,
    string NumeroDocumento,
    string? NombreComercial,
    string EstadoAutorizacion,
    DateTime? AutorizadoEn);

public sealed record UsuarioAdminItem(
    string UsuarioId,
    string NombreCompleto,
    string? Email,
    string? Telefono,
    IReadOnlyList<string> Roles,
    string Estado,
    DateTime CreadoEn,
    DateTime? UltimoAccesoEn,
    AsesorResumen? Asesor);

public sealed record UsuariosPaginaResponse(
    IReadOnlyList<UsuarioAdminItem> Items,
    int Total,
    int Pagina,
    int Tamano);
