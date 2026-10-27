namespace Camion.Api.Models;

// Tabla existente suscripciones (script 002). En P3 solo se mapea porque historial_vigencias la referencia;
// planes no se mapea todavía (plan_id queda como columna sin relación en el modelo).
public sealed class Suscripcion
{
    public const string Activa = "ACTIVA";

    public long Id { get; set; }
    public long AsesorId { get; set; }
    public long PlanId { get; set; }
    public string Estado { get; set; } = "PENDIENTE";
    public DateTime? InicioVigencia { get; set; }
    public DateTime? FinVigencia { get; set; }
    public string? AprobadoPor { get; set; }
    public DateTime? AprobadoEn { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
