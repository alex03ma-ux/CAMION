namespace Camion.Api.Models;

// P7 (HU-081, D-63 A): tabla existente configuracion_sistema del script 002 (sin migración). Una fila por clave con
// su valor en JSON; hoy solo "portal.contacto".
public sealed class ConfiguracionSistema
{
    public string Clave { get; set; } = "";
    public string ValorJson { get; set; } = "{}";
    public string? ActualizadoPor { get; set; }
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
}
