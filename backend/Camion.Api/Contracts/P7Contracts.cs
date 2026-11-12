namespace Camion.Api.Contracts;

// P7: ventas (D-62 A), pagos del plan (D-61 B), parámetros de contacto (D-63 A) y panel (D-64 A).
// Contrato: docs/criterios/P7-contrato-api.md. La validación es manual (Suscripciones/ReglasPagos.cs, Parametros/).

// ---------------------------------------------------------------- ventas

public sealed record VehiculoVenta(long Id, string Titulo, string Estado);

// asesor.id = perfiles_asesor.id; null para el asesor (como en el inventario).
public sealed record AsesorVenta(long Id, string Nombre);

public sealed record VentaItem(
    long Id,
    VehiculoVenta Vehiculo,
    AsesorVenta? Asesor,
    DateOnly FechaVenta,
    long PrecioFinalCop,
    string Origen,
    string? Observaciones,
    string Estado,
    DateTime RegistradaEn,
    string? RegistradaPor,
    DateTime? RevertidaEn,
    string? RevertidaPor,
    string? MotivoReversion);

public sealed record VentasPaginaResponse(IReadOnlyList<VentaItem> Items, int Total, int Pagina, int Tamano);

public sealed record VentasVehiculoResponse(IReadOnlyList<VentaItem> Items);

// ---------------------------------------------------------------- pagos

// valorCop es decimal para poder responder 400 a un valor con decimales; null = precio del plan.
public sealed record PagoRequest(decimal? ValorCop, string? FechaPago, string? Medio, string? Referencia);

public sealed record AnularPagoRequest(string? Motivo);

public sealed record PagoItem(
    long Id,
    long SuscripcionId,
    long ValorCop,
    DateOnly? FechaPago,
    string? Medio,
    string? Referencia,
    string? Concepto,
    string Estado,
    DateTime? RegistradoEn,
    string? RegistradoPor,
    DateTime? AnuladoEn,
    string? AnuladoPor,
    string? MotivoAnulacion);

public sealed record PagosResponse(IReadOnlyList<PagoItem> Items, long TotalPagadoCop);

// D-67: GET /api/admin/pagos/{id}/efecto-anulacion (no cambia nada).
public sealed record PlanEfecto(long Id, string Nombre);

public sealed record EfectoAnulacionResponse(
    long PagoId,
    string Tipo,
    long SuscripcionId,
    string? NuevoEstado,
    DateTime? NuevaFinVigencia,
    PlanEfecto? NuevoPlan,
    bool QuedaraVencida,
    long? SuscripcionCanceladaId,
    string? BloqueadoPor,
    string? Mensaje);

// ---------------------------------------------------------------- parámetros de contacto

public sealed record ContactoPortalRequest(
    string? Whatsapp,
    string? Correo,
    IReadOnlyList<string?>? Horario,
    string? Instagram,
    string? Tiktok);

public sealed record ContactoPortalResponse(
    string Whatsapp,
    string WhatsappEnlace,
    string Correo,
    IReadOnlyList<string> Horario,
    string? Instagram,
    string? Tiktok);

public sealed record ContactoPortalAdmin(
    string Whatsapp,
    string WhatsappEnlace,
    string Correo,
    IReadOnlyList<string> Horario,
    string? Instagram,
    string? Tiktok,
    bool Configurado,
    DateTime? ActualizadoEn,
    string? ActualizadoPor);

// ---------------------------------------------------------------- panel

public sealed record SolicitudesPorOrigen(string Origen, int Abiertas, int Nuevas);

public sealed record PanelAdminResponse(
    int AsesoresPendientes,
    IReadOnlyList<SolicitudesPorOrigen> Solicitudes,
    int SuscripcionesPorVencer,
    int SuscripcionesVencidas,
    int PublicacionesVisibles,
    IReadOnlyDictionary<string, int> VehiculosPorEstado,
    int VentasUltimos30Dias,
    DateOnly VentasDesde);
