namespace Camion.Api.Contracts;

// P4p: planes (HU-049 a HU-051), suscripciones (HU-066 a HU-069) y catálogos administrables (HU-082 a HU-084, D-37).
// Contrato: docs/criterios/P4p-contrato-api.md. La validación es manual (Suscripciones/ReglasP4p.cs).

// ---------------------------------------------------------------- planes

// POST (crear) y PUT (editar). precioCop es decimal para poder responder 400 a un valor con decimales.
public sealed record PlanRequest(
    string? Nombre,
    decimal? PrecioCop,
    long? DuracionMeses,
    long? LimitePublicaciones,
    bool? EsPrueba,
    // P4b (D-38): 0 a 20. Opcional: al crear, null = 0; al editar, null conserva el valor actual.
    long? LimiteDestacados = null);

public sealed record PlanItem(
    long Id,
    string Nombre,
    long PrecioCop,
    int DuracionMeses,
    int DiasVigencia,
    int LimitePublicaciones,
    bool EsPrueba,
    bool Activo,
    int SuscripcionesVivas,
    int LimiteDestacados);

public sealed record PlanesResponse(IReadOnlyList<PlanItem> Items);

// ---------------------------------------------------------------- suscripciones

public sealed record AsignarPlanRequest(long? PlanId);

public sealed record RenovarRequest(long? PlanId);

public sealed record MotivoSuscripcionRequest(string? Motivo);

// asesor.id = perfiles_asesor.id.
public sealed record AsesorSuscripcion(long Id, string Nombre);

public sealed record PlanSuscripcion(long Id, string Nombre, int LimitePublicaciones, int DiasVigencia, int LimiteDestacados);

public sealed record SuscripcionItem(
    long Id,
    AsesorSuscripcion Asesor,
    PlanSuscripcion Plan,
    string Estado,
    string EstadoEfectivo,
    DateTime? InicioVigencia,
    DateTime? FinVigencia,
    int? DiasRestantes,
    bool Vigente,
    DateTime? AprobadoEn,
    DateTime CreadoEn);

public sealed record SuscripcionesPaginaResponse(
    IReadOnlyList<SuscripcionItem> Items,
    int Total,
    int Pagina,
    int Tamano);

// GET /api/asesor/suscripcion (SoloAsesor).
// P4b: los destacados del asesor se consultan en GET /api/publicaciones/cupo.
public sealed record MiPlan(string Nombre, int LimitePublicaciones, int DiasVigencia);

public sealed record MiSuscripcionResponse(
    MiPlan Plan,
    string Estado,
    string EstadoEfectivo,
    DateTime? InicioVigencia,
    DateTime? FinVigencia,
    int? DiasRestantes,
    bool Vigente);

public sealed record SinSuscripcionResponse(object? Suscripcion, string Mensaje);

// ---------------------------------------------------------------- catálogos

public sealed record NombreCatalogoRequest(string? Nombre);

public sealed record LineaAdminRequest(string? Nombre, IReadOnlyList<long>? TipoIds);

public sealed record ElementoCatalogoAdmin(long Id, string Nombre, bool Activo, int Vehiculos);

public sealed record LineaCatalogoAdmin(long Id, string Nombre, bool Activo, IReadOnlyList<long> TipoIds, int Vehiculos);

public sealed record MarcaCatalogoAdmin(long Id, string Nombre, bool Activo, int Vehiculos, IReadOnlyList<LineaCatalogoAdmin> Lineas);

// Respuesta de crear o editar un modelo: aviso (o null) cuando se quita una categoría que usan vehículos.
public sealed record LineaCatalogoRespuesta(
    long Id, long MarcaId, string Nombre, bool Activo, IReadOnlyList<long> TipoIds, int Vehiculos, string? Aviso);

public sealed record CatalogoAdminResponse<T>(IReadOnlyList<T> Items);
