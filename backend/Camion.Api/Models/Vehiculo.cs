namespace Camion.Api.Models;

// P3: tabla existente vehiculos (script 002). Solo lectura. No hay columna de placa (D-2, P4).
public sealed class Vehiculo
{
    public long Id { get; set; }
    public long AsesorId { get; set; }
    public long MarcaId { get; set; }
    public long? LineaId { get; set; }
    public long TipoVehiculoId { get; set; }
    public long? CarroceriaId { get; set; }
    public long? CombustibleId { get; set; }
    public short ModeloAnio { get; set; }
    public int Kilometraje { get; set; }
    public decimal? CapacidadCargaKg { get; set; }
    public decimal? PesoBrutoKg { get; set; }
    public int? CilindrajeCc { get; set; }
    public long MunicipioId { get; set; }
    public string? DescripcionTecnica { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    public Marca? Marca { get; set; }
    public Linea? Linea { get; set; }
    public TipoVehiculo? TipoVehiculo { get; set; }
    public TipoCarroceria? Carroceria { get; set; }
    public Combustible? Combustible { get; set; }
    public Municipio? Municipio { get; set; }
}
