using System.Text.Json.Serialization;

namespace Camion.Api.Contracts;

// P4b: publicaciones (HU-023 a HU-028, HU-065), destacados (D-38), venta (HU-064, D-22, D-39) y reasignación (D-34).
// Contrato: docs/criterios/P4b-contrato-api.md. La validación es manual (Publicaciones/ReglasP4b.cs).

// POST /api/publicaciones: crea y publica. negociable null = false.
public sealed record PublicacionRequest(long? VehiculoId, string? Titulo, string? DescripcionComercial, bool? Negociable);

// PUT /api/publicaciones/{id} (HU-024): título, descripción y negociable. El precio se cambia en el vehículo (P4a).
public sealed record EditarPublicacionRequest(string? Titulo, string? DescripcionComercial, bool? Negociable);

// POST /api/publicaciones/{id}/inactivar y /api/inventario/vehiculos/{id}/revertir-venta: motivo de 5 a 500.
public sealed record MotivoPublicacionRequest(string? Motivo);

// asesor.id = perfiles_asesor.id.
public sealed record AsesorPublicacion(long Id, string Nombre);

public sealed record PublicacionItem(
    long Id,
    long VehiculoId,
    string VehiculoTitulo,
    string? Titulo,
    string? DescripcionComercial,
    bool Negociable,
    // PUBLICADA, OCULTA, VENDIDA o ELIMINADA.
    string Estado,
    // Estado más VENCIDA (PUBLICADA sin plan vigente del asesor, D-41).
    string EstadoEfectivo,
    bool Destacada,
    // Fin del plan del asesor mientras la publicación está vigente; null en los demás estados.
    DateTime? VigenteHasta,
    string SlugPublico,
    string? FotoPortada,
    // Precio del vehículo (o, si no tiene, el de la publicación).
    long? PrecioCop,
    string? MotivoOcultamiento,
    DateTime? PublicadaEn,
    DateTime ActualizadoEn,
    // Solo para el administrador.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    AsesorPublicacion? Asesor);

public sealed record MisPublicacionesPaginaResponse(
    IReadOnlyList<PublicacionItem> Items,
    int Total,
    int Pagina,
    int Tamano);

// GET /api/publicaciones/cupo. Sin plan vigente: límites en 0, planVigente false y finPlan null.
public sealed record CupoResponse(
    int LimitePublicaciones,
    int Usadas,
    int LimiteDestacados,
    int DestacadasUsadas,
    bool PlanVigente,
    DateTime? FinPlan);

// POST /api/inventario/vehiculos/{id}/vender. fechaVenta en formato AAAA-MM-DD; precioFinalCop entero > 0.
public sealed record VentaRequest(string? FechaVenta, decimal? PrecioFinalCop, string? Origen, string? Observaciones);

// POST /api/admin/vehiculos/{id}/reasignar (solo el administrador).
public sealed record ReasignarRequest(long? AsesorId, string? Motivo);

// Resumen de la publicación de un vehículo en el inventario (null si no tiene una publicación no eliminada).
public sealed record PublicacionDeVehiculo(long Id, string Estado, string EstadoEfectivo, string SlugPublico, bool Destacada);

// Venta vigente (no revertida) de un vehículo VENDIDO, en el detalle del inventario.
public sealed record VentaDeVehiculo(
    long Id,
    DateOnly FechaVenta,
    long PrecioFinalCop,
    string Origen,
    string? Observaciones,
    DateTime RegistradaEn);
