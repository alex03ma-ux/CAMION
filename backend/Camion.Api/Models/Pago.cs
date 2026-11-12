namespace Camion.Api.Models;

// P7 (D-61 B, migración 016): tabla existente pagos del script 002, ampliada. El cobro es fuera del portal; el
// administrador anota el pago y puede anularlo con motivo (la fila nunca se borra). verificado_por / verificado_en se
// usan como "registrado por / en". soporte_archivo no se mapea: no hay comprobante (D-61 B).
public sealed class Pago
{
    public const string Registrado = "REGISTRADO";
    public const string Anulado = "ANULADO";

    public const string Transferencia = "TRANSFERENCIA";
    public const string Consignacion = "CONSIGNACION";
    public const string Efectivo = "EFECTIVO";
    public const string Otro = "OTRO";
    public static readonly string[] Medios = [Transferencia, Consignacion, Efectivo, Otro];

    public const string ConceptoAsignacion = "ASIGNACION";
    public const string ConceptoRenovacion = "RENOVACION";
    public const string ConceptoCambioPlan = "CAMBIO_PLAN";
    public const string ConceptoPosterior = "POSTERIOR";

    public long Id { get; set; }
    public long SuscripcionId { get; set; }
    public decimal ValorCop { get; set; }
    public DateOnly? FechaPago { get; set; }
    public string? Medio { get; set; }
    public string? Referencia { get; set; }
    public string? Concepto { get; set; }
    public string Estado { get; set; } = Registrado;
    public string? RegistradoPor { get; set; }
    public DateTime? RegistradoEn { get; set; }
    public string? AnuladoPor { get; set; }
    public DateTime? AnuladoEn { get; set; }
    public string? MotivoAnulacion { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
