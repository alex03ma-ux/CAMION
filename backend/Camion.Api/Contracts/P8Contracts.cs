namespace Camion.Api.Contracts;

// P8: reportes (D-68 A), indicadores (D-69 A), "Mis indicadores" (D-70 A), exportación CSV (D-71), actividad (D-72 A) y
// recuperación asistida de contraseña (D-73 D). Contrato: docs/criterios/P8-contrato-api.md.
// Ningún DTO lleva nombre, teléfono, correo, mensaje, notas ni observaciones de clientes (RN-SE-004, D-71 b A).

// ---------------------------------------------------------------- comunes

public sealed record Periodo(DateOnly Desde, DateOnly Hasta);

public sealed record PaginaFilas<T>(IReadOnlyList<T> Items, int Total, int Pagina, int Tamano);

public sealed record Referencia(long Id, string Nombre);

public sealed record VehiculoRef(long Id, string Titulo);

public sealed record ConteoNombre(long Id, string Nombre, int Total);

public sealed record CantidadValor(int Cantidad, long ValorCop);

public sealed record ConteoValorNombre(long Id, string Nombre, int Cantidad, long ValorCop);

public sealed record ConteoValorCodigo(string Codigo, int Cantidad, long ValorCop);

// ---------------------------------------------------------------- vehículos y publicaciones (HU-076)

public sealed record PublicacionFila(long Id, string Estado);

public sealed record FilaVehiculo(
    long VehiculoId,
    string Titulo,
    string Categoria,
    string Estado,
    Referencia Asesor,
    string Municipio,
    string Departamento,
    DateTime RegistradoEn,
    PublicacionFila? Publicacion,
    int ContactosEnPeriodo);

public sealed record MasContactado(long VehiculoId, string Titulo, int Contactos);

public sealed record ReporteVehiculosResponse(
    Periodo Periodo,
    IReadOnlyDictionary<string, int> InventarioPorEstado,
    IReadOnlyList<ConteoNombre> InventarioPorCategoria,
    int RegistradosEnPeriodo,
    IReadOnlyDictionary<string, int> PublicacionesPorEstado,
    int PublicacionesEliminadas,
    int PublicacionesVisiblesHoy,
    int PublicacionesDestacadasHoy,
    IReadOnlyList<MasContactado> MasContactados,
    PaginaFilas<FilaVehiculo> Filas);

// ---------------------------------------------------------------- solicitudes (HU-077)

public sealed record SolicitudesOrigen(string Origen, int Recibidas, int Abiertas, int Cerradas);

public sealed record CerradasResultado(string Origen, string Resultado, int Total);

public sealed record TiempoCierre(string Origen, int Cerradas, double? Dias);

public sealed record ContactosPorResponsable(
    int Administracion,
    IReadOnlyList<ConteoNombre> Asesores,
    int ReasignadosAMano,
    int PasadosAAdministracion);

public sealed record ResponsableFila(string Tipo, long? AsesorId, string Nombre);

public sealed record FilaSolicitud(
    long Id,
    DateTime CreadoEn,
    string Origen,
    string Estado,
    string? Resultado,
    DateTime? CerradoEn,
    ResponsableFila Responsable,
    VehiculoRef? Vehiculo);

public sealed record ReporteSolicitudesResponse(
    Periodo Periodo,
    int Recibidas,
    IReadOnlyList<SolicitudesOrigen> PorOrigen,
    int CerradasEnPeriodo,
    IReadOnlyList<CerradasResultado> CerradasPorResultado,
    IReadOnlyList<TiempoCierre> TiempoMedioCierre,
    ContactosPorResponsable ContactosPorResponsable,
    PaginaFilas<FilaSolicitud> Filas);

// ---------------------------------------------------------------- créditos (HU-078)

public sealed record ConteoResultado(string Resultado, int Total);

public sealed record FilaCredito(
    long Id,
    DateTime CreadoEn,
    string Canal,
    string Estado,
    string? Resultado,
    DateTime? CerradoEn,
    VehiculoRef? Vehiculo);

public sealed record ReporteCreditosResponse(
    Periodo Periodo,
    int Recibidos,
    IReadOnlyDictionary<string, int> PorEstado,
    IReadOnlyDictionary<string, int> PorCanal,
    int Cerrados,
    IReadOnlyList<ConteoResultado> CerradosPorResultado,
    double? PorcentajeRemitido,
    PaginaFilas<FilaCredito> Filas);

// ---------------------------------------------------------------- ventas (HU-080, D-39)

public sealed record VentasOrigen(string Origen, int Cantidad, long ValorCop);

public sealed record FilaVenta(
    long Id,
    DateOnly FechaVenta,
    VehiculoRef Vehiculo,
    string Categoria,
    Referencia Asesor,
    string Origen,
    long PrecioFinalCop,
    string Estado);

public sealed record ReporteVentasResponse(
    Periodo Periodo,
    CantidadValor Vigentes,
    int Revertidas,
    IReadOnlyList<VentasOrigen> PorOrigen,
    IReadOnlyList<ConteoValorNombre> PorCategoria,
    IReadOnlyList<ConteoValorNombre> PorAsesor,
    PaginaFilas<FilaVenta> Filas);

// ---------------------------------------------------------------- suscripciones y pagos (D-61 B, D-66, D-67)

public sealed record ResumenPagos(
    int Cantidad,
    long TotalRecibidoCop,
    IReadOnlyList<ConteoValorCodigo> PorMedio,
    IReadOnlyList<ConteoValorNombre> PorPlan,
    IReadOnlyList<ConteoValorCodigo> PorConcepto,
    CantidadValor Anulados,
    int SinFechaPago);

public sealed record FilaPago(
    long Id,
    DateOnly Fecha,
    bool SinFechaPago,
    long SuscripcionId,
    Referencia Asesor,
    Referencia Plan,
    string Medio,
    string Concepto,
    long ValorCop,
    string Estado);

public sealed record ReporteSuscripcionesResponse(
    Periodo Periodo,
    IReadOnlyDictionary<string, int> PorEstado,
    IReadOnlyList<ConteoNombre> VigentesPorPlan,
    int AsignadasEnPeriodo,
    int RenovadasEnPeriodo,
    int CambiosDePlanEnPeriodo,
    int VencidasEnPeriodo,
    ResumenPagos Pagos,
    string Aviso,
    PaginaFilas<FilaPago> Filas);

// ---------------------------------------------------------------- indicadores (D-69 A/A)

public sealed record Indicador(long Actual, long Anterior, long Diferencia);

public sealed record IndicadorDecimal(double? Actual, double? Anterior, double? Diferencia);

public sealed record IndicadoresValores(
    Indicador ContactosRecibidos,
    IndicadorDecimal PorcentajeVentaConcretada,
    Indicador VentasVigentes,
    Indicador ValorVendidoCop,
    Indicador SolicitudesAdministracion,
    Indicador CreditosRemitidos,
    Indicador IngresosPlanesCop,
    IndicadorDecimal TiempoMedioCierreDias);

public sealed record OrigenComparado(string Origen, int Actual, int Anterior);

public sealed record VentasMes(string Mes, int Cantidad, long ValorCop);

public sealed record ConteoOrigen(string Origen, int Total);

public sealed record GraficosIndicadores(
    IReadOnlyList<VentasMes> VentasPorMes,
    IReadOnlyList<VentasOrigen> VentasPorOrigen,
    IReadOnlyList<ConteoOrigen> SolicitudesPorOrigen,
    IReadOnlyList<ConteoResultado> ContactosPorResultado);

public sealed record IndicadoresAdminResponse(
    Periodo Periodo,
    Periodo PeriodoAnterior,
    IndicadoresValores Indicadores,
    IReadOnlyList<OrigenComparado> SolicitudesAdministracionPorOrigen,
    int PublicacionesVisiblesHoy,
    GraficosIndicadores Graficos);

// D-70 A: solo lo del asesor; sin dinero de planes, avisos, créditos, búsquedas ni comparaciones.
public sealed record IndicadoresAsesorResponse(
    Periodo Periodo,
    IReadOnlyDictionary<string, int> VehiculosPorEstado,
    int PublicacionesVisiblesHoy,
    int ContactosRecibidos,
    int ContactosAbiertos,
    IReadOnlyList<ConteoResultado> ContactosPorResultado,
    CantidadValor VentasVigentes);

// ---------------------------------------------------------------- actividad (D-72 A)

public sealed record CambioActividad(string Campo, string? Anterior, string? Nuevo);

public sealed record EventoActividad(
    long Id,
    DateTime Fecha,
    string Quien,
    string Sobre,
    long SobreId,
    string Accion,
    string Descripcion,
    string? Motivo,
    IReadOnlyList<CambioActividad> Cambios);

// ---------------------------------------------------------------- recuperación asistida (D-73 D)

public sealed record EnlaceRecuperacionResponse(string Enlace, DateTime VenceEn, string Mensaje);

public sealed record RestablecerContrasenaRequest(string? Codigo, string? Password);

public sealed record MensajeResponse(string Mensaje);
