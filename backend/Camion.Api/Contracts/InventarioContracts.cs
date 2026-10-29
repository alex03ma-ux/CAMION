using System.Text.Json.Serialization;

namespace Camion.Api.Contracts;

// P4a (HU-013 a HU-022, RN-VE-001 a RN-VE-010, RN-SE-006, D-2, D-7, D-17, D-19, D-20): inventario de vehículos.
// Contrato: docs/criterios/P4a-contrato-api.md. La validación es manual (ReglasInventario).

// Cuerpo de POST (crear) y PUT (editar). En PUT, asesorId se ignora; el tipo no se puede cambiar.
public sealed record VehiculoInventarioRequest(
    long? TipoId,
    long? MarcaId,
    long? LineaId,
    int? ModeloAnio,
    long? Kilometraje,
    long? HorasUso,
    string? Placa,
    long? CarroceriaId,
    long? CombustibleId,
    long? CilindrajeCc,
    decimal? PesoBrutoKg,
    decimal? CapacidadCargaKg,
    decimal? LargoM,
    decimal? AnchoM,
    decimal? AltoM,
    long? MaterialId,
    long? TipoMaquinaId,
    long? MunicipioId,
    decimal? PrecioCop,
    string? DescripcionTecnica,
    long? AsesorId);

// Cuerpo de reservar / liberar / inactivar / reactivar. El motivo es obligatorio para reservar (nota, D-28) e inactivar (5 a 500).
public sealed record AccionVehiculoRequest(string? Motivo);

public sealed record OrdenFotosRequest(IReadOnlyList<long>? FotoIds);

// asesorId de inventario = perfiles_asesor.id (el mismo valor que vehiculos.asesor_id).
public sealed record AsesorInventario(long Id, string Nombre);

public sealed record FotoInventarioResponse(long Id, string Url, int Orden, bool EsPortada);

public sealed record ItemInventario(
    long Id,
    string Tipo,
    string? Marca,
    string? Linea,
    int? ModeloAnio,
    string? Placa,
    long? PrecioCop,
    string Estado,
    int? Kilometraje,
    int? HorasUso,
    string Municipio,
    string? FotoPortada,
    int TotalFotos,
    DateTime ActualizadoEn,
    string? NotaReserva,
    // Solo para el administrador.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    AsesorInventario? Asesor,
    // P4b: su publicación no eliminada (se omite si no tiene).
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    PublicacionDeVehiculo? Publicacion = null);

public sealed record InventarioPaginaResponse(
    IReadOnlyList<ItemInventario> Items,
    int Total,
    int Pagina,
    int Tamano);

public sealed record DetalleInventario(
    long Id,
    long TipoId,
    string Tipo,
    long? MarcaId,
    string? Marca,
    long? LineaId,
    string? Linea,
    int? ModeloAnio,
    int? Kilometraje,
    int? HorasUso,
    string? Placa,
    long? CarroceriaId,
    string? Carroceria,
    long? CombustibleId,
    string? Combustible,
    int? CilindrajeCc,
    decimal? PesoBrutoKg,
    decimal? CapacidadCargaKg,
    decimal? LargoM,
    decimal? AnchoM,
    decimal? AltoM,
    long? MaterialId,
    string? Material,
    long? TipoMaquinaId,
    string? TipoMaquina,
    long MunicipioId,
    string Municipio,
    long DepartamentoId,
    string Departamento,
    long? PrecioCop,
    long? PrecioAnteriorCop,
    string? DescripcionTecnica,
    string Estado,
    string? MotivoInactivacion,
    string? NotaReserva,
    DateTime CreadoEn,
    DateTime ActualizadoEn,
    IReadOnlyList<FotoInventarioResponse> Fotos,
    // Solo para el administrador.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    AsesorInventario? Asesor,
    // P4b: su publicación no eliminada (se omite si no tiene).
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    PublicacionDeVehiculo? Publicacion = null,
    // P4b: la venta vigente (no revertida) si el vehículo está VENDIDO (se omite si no hay).
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    VentaDeVehiculo? Venta = null);

public sealed record OpcionCatalogo(long Id, string Nombre);

// D-32: cada modelo trae sus categorías; vacía = sin clasificar (se ofrece en todas).
public sealed record LineaCatalogo(long Id, string Nombre, IReadOnlyList<long> TipoIds);

public sealed record MarcaCatalogo(long Id, string Nombre, IReadOnlyList<LineaCatalogo> Lineas);

public sealed record DepartamentoCatalogo(long Id, string Nombre, IReadOnlyList<OpcionCatalogo> Municipios);

public sealed record CatalogosInventarioResponse(
    IReadOnlyList<OpcionCatalogo> Tipos,
    IReadOnlyList<MarcaCatalogo> Marcas,
    IReadOnlyList<OpcionCatalogo> Carrocerias,
    IReadOnlyList<OpcionCatalogo> Materiales,
    IReadOnlyList<OpcionCatalogo> TiposMaquina,
    IReadOnlyList<OpcionCatalogo> Combustibles,
    IReadOnlyList<DepartamentoCatalogo> Departamentos,
    // Solo para el administrador: asesores con cuenta ACTIVA.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<AsesorInventario>? Asesores);
