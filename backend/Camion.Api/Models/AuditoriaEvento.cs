namespace Camion.Api.Models;

// Tabla existente auditoria_eventos (HU-061, RN-SE-003). DatosAnteriores/DatosNuevos guardan JSON serializado;
// nunca contraseñas, tokens ni el documento completo.
public sealed class AuditoriaEvento
{
    public long Id { get; set; }
    public string? UsuarioId { get; set; }
    public string Entidad { get; set; } = "";
    public string EntidadId { get; set; } = "";
    public string Accion { get; set; } = "";
    public string? DatosAnteriores { get; set; }
    public string? DatosNuevos { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
