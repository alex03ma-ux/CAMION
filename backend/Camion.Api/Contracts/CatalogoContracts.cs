namespace Camion.Api.Contracts;

// P3 (HU-001 a HU-012, RN-VE-011 a RN-VE-014, RN-SE-005): catálogo público, anónimo y de solo lectura.
// Ninguna respuesta incluye datos del asesor, ids internos de vehículo/asesor, la placa ni datos personales.
// Única excepción (P5, D-47): el detalle trae "whatsapp", el enlace wa.me del asesor dueño (o el del portal); nunca su
// correo, documento, nombre ni id.
// P4a (D-19): marca y modeloAnio pueden ser null (carrocería sin marca ni año).
public sealed record TarjetaVehiculo(
    string Slug,
    string Titulo,
    long PrecioCop,
    bool Negociable,
    string EstadoComercial,
    bool Destacado,
    string Tipo,
    string? Marca,
    string? Linea,
    int? ModeloAnio,
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
    string? Marca,
    string? Linea,
    int? ModeloAnio,
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
    IReadOnlyList<FotoVehiculo> Fotos,
    // P5 (D-47, CA-P5-61, CA-P5-62): https://wa.me/57XXXXXXXXXX del asesor dueño si su teléfono es un celular
    // colombiano válido; si no, o si el dueño es administrador, el del portal (D-42).
    string Whatsapp,
    // P5 (CA-P5-11): true solo si la petición trae un token válido del asesor dueño; sin token o de otro usuario, false.
    bool EsPropio);

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

// P6 (HU-034, D-17, D-29, D-32): GET /api/catalogo/listas-busqueda. Categorías, marcas con sus modelos (tipoIds = categorías
// activas del modelo; vacía = sin clasificar, vale para todas) y departamentos con sus municipios. Solo entradas activas.
public sealed record ListasBusquedaResponse(
    IReadOnlyList<OpcionCatalogo> Tipos,
    IReadOnlyList<MarcaCatalogo> Marcas,
    IReadOnlyList<DepartamentoCatalogo> Departamentos);
