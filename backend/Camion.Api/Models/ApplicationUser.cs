using Microsoft.AspNetCore.Identity;

namespace Camion.Api.Models;

public sealed class ApplicationUser : IdentityUser
{
    public string NombreCompleto { get; set; } = "";
    public string EstadoCuenta { get; set; } = "ACTIVA";
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? UltimoAccesoEn { get; set; }
    public int SessionVersion { get; set; }
}
