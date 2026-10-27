namespace Camion.Api.Contracts;

// P3 (HU-001 a HU-012, RN-VE-011 a RN-VE-014, RN-SE-005): catálogo público, anónimo y de solo lectura.
// Ninguna respuesta incluye datos del asesor, ids internos de vehículo/asesor ni datos personales.
public sealed record TarjetaVehiculo(
    string Slug,
    string Titulo,
    long PrecioCop,
    bool Negociable,
    string EstadoComercial,
    bool Destacado,
    string Tipo,
    string Marca,
    string? Linea,
    int ModeloAnio,
    int Kilometraje,
    string Municipio,
    string Departamento,
    string? FotoPortada,
    DateTime PublicadaEn);

public sealed record FotoVehiculo(string Url, int Orden, bool EsPortada);

// Detalle = tarjeta + ficha técnica + fotos (P3-contrato-api.md).
public sealed record DetalleVehiculo(
    string Slug,
    string Titulo,
    long PrecioCop,
    bool Negociable,
    string EstadoComercial,
    bool Destacado,
    string Tipo,
    string Marca,
    string? Linea,
    int ModeloAnio,
    int Kilometraje,
    string Municipio,
    string Departamento,
    string? FotoPortada,
    DateTime PublicadaEn,
    string? DescripcionComercial,
    string? DescripcionTecnica,
    string? Carroceria,
    string? Combustible,
    decimal? CapacidadCargaKg,
    decimal? PesoBrutoKg,
    int? CilindrajeCc,
    IReadOnlyList<FotoVehiculo> Fotos);

public sealed record InicioCatalogoResponse(
    IReadOnlyList<TarjetaVehiculo> Destacados,
    IReadOnlyList<TarjetaVehiculo> Recientes);

public sealed record PublicacionesPaginaResponse(
    IReadOnlyList<TarjetaVehiculo> Items,
    int Total,
    int Pagina,
    int Tamano);

public sealed record OpcionFiltro(long Id, string Nombre, int Total);

public sealed record MarcaFiltro(long Id, string Nombre, int Total, IReadOnlyList<OpcionFiltro> Lineas);

public sealed record RangosFiltro(long? PrecioMin, long? PrecioMax, int? AnioMin, int? AnioMax, int? KmMax);

public sealed record FiltrosCatalogoResponse(
    IReadOnlyList<OpcionFiltro> Tipos,
    IReadOnlyList<MarcaFiltro> Marcas,
    RangosFiltro Rangos);
