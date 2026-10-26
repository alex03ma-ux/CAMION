namespace Camion.Api.Models;

// Tabla existente historial_autorizaciones: un registro por cada cambio de estado de un asesor.
public sealed class HistorialAutorizacion
{
    public long Id { get; set; }
    public long AsesorId { get; set; }
    public string? EstadoAnterior { get; set; }
    public string EstadoNuevo { get; set; } = "";
    public string? AdministradorId { get; set; }
    public string? Motivo { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
