namespace Camion.Api.Models;

// P3: tablas existentes publicaciones, fotos_publicacion, historial_vigencias y publicaciones_destacadas
// (script 002). Solo lectura. Estados D-4 / D-11.
public sealed class Publicacion
{
    public const string Publicada = "PUBLICADA";
    public const string Disponible = "DISPONIBLE";
    public const string Reservado = "RESERVADO";
    public const string Vendido = "VENDIDO";

    public long Id { get; set; }
    public long VehiculoId { get; set; }
    public long AsesorId { get; set; }
    public string SlugPublico { get; set; } = "";
    public string? Titulo { get; set; }
    public string? DescripcionComercial { get; set; }
    public decimal? PrecioCop { get; set; }
    public bool Negociable { get; set; }
    public string EstadoComercial { get; set; } = Disponible;
    public string EstadoPublicacion { get; set; } = "BORRADOR";
    public string? MotivoOcultamiento { get; set; }
    public DateTime? PublicadaEn { get; set; }
    public DateTime? OcultadaEn { get; set; }
    public DateTime? EliminadaEn { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    public Vehiculo? Vehiculo { get; set; }
    public List<FotoPublicacion> Fotos { get; set; } = [];
    public List<HistorialVigencia> Vigencias { get; set; } = [];
    public List<PublicacionDestacada> Destacados { get; set; } = [];
}

public sealed class FotoPublicacion
{
    public long Id { get; set; }
    public long PublicacionId { get; set; }
    public string ArchivoClave { get; set; } = "";
    public byte Orden { get; set; }
    public bool EsPortada { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}

public sealed class HistorialVigencia
{
    public long Id { get; set; }
    public long PublicacionId { get; set; }
    public long SuscripcionId { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime Fin { get; set; }
    public string Motivo { get; set; } = "";
}

public sealed class PublicacionDestacada
{
    public const string Activa = "ACTIVA";

    public long Id { get; set; }
    public long PublicacionId { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime Fin { get; set; }
    public string Estado { get; set; } = Activa;
    public string? CreadoPor { get; set; }
}
