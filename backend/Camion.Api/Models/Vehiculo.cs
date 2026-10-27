namespace Camion.Api.Models;

// Tabla existente vehiculos (script 002) con las columnas de la migración 004 (P4a, HU-013 a HU-022).
// D-2: placa opcional y única. D-4/D-11: estado del vehículo. D-19: marca y año opcionales (carrocería),
// medidas y material (carrocería), horas de uso y tipo de máquina (maquinaria). RN-VE-007: todo vehículo tiene asesor.
public sealed class Vehiculo
{
    public const string Activo = "ACTIVO";
    public const string Reservado = "RESERVADO";
    public const string Inactivo = "INACTIVO";
    public const string Vendido = "VENDIDO";

    public static readonly string[] Estados = [Activo, Reservado, Inactivo, Vendido];

    public long Id { get; set; }
    public long AsesorId { get; set; }
    public string? Placa { get; set; }
    public string Estado { get; set; } = Activo;
    public decimal? PrecioCop { get; set; }
    public decimal? PrecioAnteriorCop { get; set; }
    public long? MarcaId { get; set; }
    public long? LineaId { get; set; }
    public long TipoVehiculoId { get; set; }
    public long? CarroceriaId { get; set; }
    public long? CombustibleId { get; set; }
    public short? ModeloAnio { get; set; }
    public int Kilometraje { get; set; }
    public decimal? CapacidadCargaKg { get; set; }
    public decimal? PesoBrutoKg { get; set; }
    public int? CilindrajeCc { get; set; }
    public decimal? LargoM { get; set; }
    public decimal? AnchoM { get; set; }
    public decimal? AltoM { get; set; }
    public long? MaterialId { get; set; }
    public int? HorasUso { get; set; }
    public long? TipoMaquinaId { get; set; }
    public long MunicipioId { get; set; }
    public string? DescripcionTecnica { get; set; }
    public string? MotivoInactivacion { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    public Marca? Marca { get; set; }
    public Linea? Linea { get; set; }
    public TipoVehiculo? TipoVehiculo { get; set; }
    public TipoCarroceria? Carroceria { get; set; }
    public Combustible? Combustible { get; set; }
    public MaterialCarroceria? Material { get; set; }
    public TipoMaquina? TipoMaquina { get; set; }
    public Municipio? Municipio { get; set; }
    public List<FotoInventario> Fotos { get; set; } = [];
}

// P4a (HU-014, RN-VE-001, D-20): tabla nueva fotos_vehiculo. Las fotos son del vehículo; fotos_publicacion no se toca.
public sealed class FotoInventario
{
    public const int Maximo = 10;

    public long Id { get; set; }
    public long VehiculoId { get; set; }
    public string ArchivoClave { get; set; } = "";
    public byte Orden { get; set; }
    public bool EsPortada { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
