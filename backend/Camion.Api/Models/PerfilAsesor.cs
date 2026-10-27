namespace Camion.Api.Models;

// Tabla existente perfiles_asesor (002_esquema_inicial_mariadb_10_4.sql). Estados D-10:
// PENDIENTE / APROBADO / RECHAZADO / BLOQUEADO (requieren la migración 003 en la base real).
public sealed class PerfilAsesor
{
    public const string Pendiente = "PENDIENTE";
    public const string Aprobado = "APROBADO";
    public const string Rechazado = "RECHAZADO";
    public const string Bloqueado = "BLOQUEADO";

    public long Id { get; set; }
    public string UsuarioId { get; set; } = "";
    public string TipoDocumento { get; set; } = "";
    public string NumeroDocumento { get; set; } = "";
    public string? NombreComercial { get; set; }
    public string EstadoAutorizacion { get; set; } = Pendiente;
    public string? AutorizadoPor { get; set; }
    public DateTime? AutorizadoEn { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
