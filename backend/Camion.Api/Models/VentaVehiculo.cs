namespace Camion.Api.Models;

// P4b (HU-064, D-22, D-39, migración 012): tabla nueva ventas_vehiculo. Una fila por venta; revertirla no la borra,
// la marca con quién, cuándo y por qué.
public sealed class VentaVehiculo
{
    public const string OrigenPortal = "PORTAL";
    public const string OrigenContactoAsesor = "CONTACTO_ASESOR";
    public const string OrigenReferido = "REFERIDO";
    public const string OrigenOtro = "OTRO";

    public static readonly string[] Origenes = [OrigenPortal, OrigenContactoAsesor, OrigenReferido, OrigenOtro];

    public long Id { get; set; }
    public long VehiculoId { get; set; }
    public long? PublicacionId { get; set; }
    public DateOnly FechaVenta { get; set; }
    public decimal PrecioFinalCop { get; set; }
    public string Origen { get; set; } = OrigenPortal;
    public string? Observaciones { get; set; }
    public string? RegistradaPor { get; set; }
    public DateTime RegistradaEn { get; set; } = DateTime.UtcNow;
    public string? RevertidaPor { get; set; }
    public DateTime? RevertidaEn { get; set; }
    public string? MotivoReversion { get; set; }
}
