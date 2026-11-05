namespace Camion.Api.Contracts;

// P5: contactos y leads (HU-029 a HU-033, D-7, D-9, D-44, D-46, D-47, D-48). Contrato: docs/criterios/P5-contrato-api.md.
// La validación es manual (Leads/ReglasLeads.cs).

// POST /api/contactos (anónimo). La publicación se identifica por su slug (el portal no expone ids). sitioWeb es el campo
// trampa: debe llegar vacío. Cualquier otro campo (responsable, estado, versión de la política) se ignora (CA-P5-08, 15).
public sealed record ContactoRequest(
    string? Slug,
    string? Nombre,
    string? Telefono,
    string? Correo,
    string? Mensaje,
    bool? AutorizacionDatos,
    string? SitioWeb);

// 201: el mismo mensaje para un lead nuevo, un contacto repetido o el campo trampa (CA-P5-54). Sin id ni responsable.
public sealed record ContactoResponse(string Mensaje);

// POST /api/leads/{id}/notas
public sealed record NotaLeadRequest(string? Texto);

// POST /api/leads/{id}/cerrar
public sealed record CerrarLeadRequest(string? Resultado, string? Observacion);

// POST /api/leads/{id}/reabrir
public sealed record ReabrirLeadRequest(string? Motivo);

// POST /api/leads/{id}/reasignar: administracion = true (sin asesorId) o asesorId = perfiles_asesor.id.
public sealed record ReasignarLeadRequest(long? AsesorId, bool? Administracion, string? Motivo);

// Responsable: tipo ASESOR (asesorId = perfiles_asesor.id, nombre) o ADMINISTRACION (sin asesorId ni nombre).
public sealed record ResponsableLead(string Tipo, long? AsesorId, string? Nombre);

public sealed record VehiculoLead(
    long VehiculoId,
    long PublicacionId,
    string Titulo,
    string Slug,
    // ACTIVO, RESERVADO, INACTIVO o VENDIDO (estado actual, CA-P5-49).
    string EstadoVehiculo,
    // PUBLICADA, OCULTA, ELIMINADA o VENDIDA.
    string EstadoPublicacion);

public sealed record LeadItem(
    long Id,
    string Estado,
    string Nombre,
    string Telefono,
    string? Correo,
    string Mensaje,
    DateTime CreadoEn,
    // D-48: abierto y aún no visto por su responsable (solo si quien consulta es el responsable).
    bool Nuevo,
    VehiculoLead Vehiculo,
    ResponsableLead Responsable,
    string? ResultadoCierre,
    DateTime? CerradoEn);

public sealed record LeadsPaginaResponse(IReadOnlyList<LeadItem> Items, int Total, int Pagina, int Tamano);

public sealed record LeadsResumenResponse(int Nuevos, int Abiertos);

// Actor: USUARIO (con nombre) o SISTEMA.
public sealed record ActorEvento(string Tipo, string? Nombre);

public sealed record EventoLeadItem(
    string Tipo,
    DateTime Fecha,
    ActorEvento Actor,
    bool Automatico,
    string? Texto,
    // Solo en CERRADO.
    string? Resultado,
    ResponsableLead? ResponsableAnterior,
    ResponsableLead? ResponsableNuevo);

public sealed record ConsentimientoItem(string Formulario, string VersionPolitica, DateTime AceptadoEn);

public sealed record LeadDetalle(
    long Id,
    string Estado,
    string Nombre,
    string Telefono,
    string? Correo,
    string Mensaje,
    DateTime CreadoEn,
    bool Nuevo,
    VehiculoLead Vehiculo,
    ResponsableLead Responsable,
    string? ResultadoCierre,
    DateTime? CerradoEn,
    string? ObservacionCierre,
    string? CerradoPor,
    IReadOnlyList<EventoLeadItem> Eventos,
    // Solo para el administrador; null para el asesor (CA-P5-16).
    IReadOnlyList<ConsentimientoItem>? Consentimientos);
