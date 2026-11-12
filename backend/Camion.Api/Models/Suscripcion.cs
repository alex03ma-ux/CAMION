namespace Camion.Api.Models;

// Tabla existente suscripciones (script 002). P3 la mapeó porque historial_vigencias la referencia; P4p (HU-066 a
// HU-069, D-4) la gestiona. SUSPENDIDA y REACTIVADA requieren la migración 011 en la base real.
public sealed class Suscripcion
{
    public const string Pendiente = "PENDIENTE";
    public const string Activa = "ACTIVA";
    public const string Suspendida = "SUSPENDIDA";
    public const string Reactivada = "REACTIVADA";
    public const string Vencida = "VENCIDA";
    public const string Cancelada = "CANCELADA";
    public const string Rechazada = "RECHAZADA";

    public static readonly string[] Estados = [Pendiente, Activa, Suspendida, Reactivada, Vencida, Cancelada, Rechazada];

    // Contrato P4p: un asesor tiene como máximo una suscripción "viva".
    public static readonly string[] EstadosVivos = [Activa, Reactivada, Suspendida];

    public long Id { get; set; }
    public long AsesorId { get; set; }
    public long PlanId { get; set; }
    public string Estado { get; set; } = Pendiente;
    public DateTime? InicioVigencia { get; set; }
    public DateTime? FinVigencia { get; set; }
    public string? AprobadoPor { get; set; }
    public DateTime? AprobadoEn { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public Plan? Plan { get; set; }
}

// Tabla existente planes (script 002). P4p: HU-049 a HU-051 (RN-PL-001, RN-PL-002). Un plan nunca se borra.
public sealed class Plan
{
    public long Id { get; set; }
    public string Nombre { get; set; } = "";
    public decimal PrecioCop { get; set; }
    public int DuracionMeses { get; set; }
    public int LimitePublicaciones { get; set; }
    // P4b (D-38, migración 012): destacados incluidos en el plan, de 0 a 20.
    public int LimiteDestacados { get; set; }
    public bool EsPrueba { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
