using System.Globalization;
using Camion.Api.Leads;
using Camion.Api.Models;

namespace Camion.Api.Reportes;

// P8: nombres en palabras de los códigos, para el CSV (D-71) y la actividad (D-72 A). Los mismos que ve el
// administrador en la bandeja, el inventario, las ventas y las suscripciones.
public static class Etiquetas
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    public static readonly IReadOnlyDictionary<string, string> OrigenLead = new Dictionary<string, string>
    {
        [Lead.OrigenContactoVehiculo] = "Contacto de vehículo",
        [Lead.OrigenVendeTuCamion] = "Vende tu camión",
        [Lead.OrigenSeguroTodoRiesgo] = "Seguro todo riesgo",
        [Lead.OrigenCredito] = "Crédito",
        [Lead.OrigenBusqueda] = "Búsqueda"
    };

    public static readonly IReadOnlyDictionary<string, string> Resultado = new Dictionary<string, string>
    {
        [Lead.VentaConcretada] = "Venta concretada",
        [Lead.NoInteresado] = "No le interesó",
        [Lead.NoRespondio] = "No respondió",
        [Lead.VehiculoNoDisponible] = "Vehículo no disponible",
        [Lead.DuplicadoInvalido] = "Contacto duplicado o inválido",
        [Lead.Otro] = "Otro",
        [Lead.Atendida] = "Solicitud atendida",
        [Lead.RemitidoEntidad] = "Remitido a entidad financiera",
        [Lead.VehiculoEncontrado] = "Encontró vehículo",
        [Lead.SinVehiculo] = "Sin vehículo disponible"
    };

    public static readonly IReadOnlyDictionary<string, string> EstadoLead = new Dictionary<string, string>
    {
        [Lead.Abierto] = "Abierto",
        [Lead.Cerrado] = "Cerrado",
        [ReglasLeads.EstadoRegistrado] = "Registrado",
        [ReglasLeads.EstadoEnGestion] = "En gestión"
    };

    public static readonly IReadOnlyDictionary<string, string> OrigenVenta = new Dictionary<string, string>
    {
        [VentaVehiculo.OrigenPortal] = "Portal web",
        [VentaVehiculo.OrigenContactoAsesor] = "Contacto directo del asesor",
        [VentaVehiculo.OrigenReferido] = "Referido",
        [VentaVehiculo.OrigenOtro] = "Otro"
    };

    public static readonly IReadOnlyDictionary<string, string> EstadoVehiculo = new Dictionary<string, string>
    {
        [Vehiculo.Activo] = "Activo",
        [Vehiculo.Reservado] = "Reservado",
        [Vehiculo.Inactivo] = "Inactivo",
        [Vehiculo.Vendido] = "Vendido"
    };

    public static readonly IReadOnlyDictionary<string, string> EstadoPublicacion = new Dictionary<string, string>
    {
        [Publicacion.Borrador] = "Borrador",
        [Publicacion.Publicada] = "Publicada",
        [Publicacion.Oculta] = "Oculta",
        [Publicacion.Eliminada] = "Eliminada",
        ["VENDIDA"] = "Vendida",
        ["VENCIDA"] = "Vencida"
    };

    public static readonly IReadOnlyDictionary<string, string> EstadoSuscripcion = new Dictionary<string, string>
    {
        [Suscripcion.Pendiente] = "Pendiente",
        [Suscripcion.Activa] = "Activa",
        [Suscripcion.Suspendida] = "Suspendida",
        [Suscripcion.Reactivada] = "Reactivada",
        [Suscripcion.Vencida] = "Vencida",
        [Suscripcion.Cancelada] = "Cancelada",
        [Suscripcion.Rechazada] = "Rechazada"
    };

    public const string SinDato = "SIN_DATO";

    public static readonly IReadOnlyDictionary<string, string> Medio = new Dictionary<string, string>
    {
        [Pago.Transferencia] = "Transferencia",
        [Pago.Consignacion] = "Consignación",
        [Pago.Efectivo] = "Efectivo",
        [Pago.Otro] = "Otro",
        [SinDato] = "Sin dato"
    };

    public static readonly IReadOnlyDictionary<string, string> Concepto = new Dictionary<string, string>
    {
        [Pago.ConceptoAsignacion] = "Asignación",
        [Pago.ConceptoRenovacion] = "Renovación",
        [Pago.ConceptoCambioPlan] = "Cambio de plan",
        [Pago.ConceptoPosterior] = "Pago posterior",
        [SinDato] = "Sin dato"
    };

    public static readonly IReadOnlyDictionary<string, string> Otras = new Dictionary<string, string>
    {
        [Pago.Registrado] = "Registrado",
        [Pago.Anulado] = "Anulado",
        ["VIGENTE"] = "Vigente",
        ["REVERTIDA"] = "Revertida",
        [FiltrosReporte.CanalPortada] = "Portada",
        [FiltrosReporte.CanalFicha] = "Ficha del vehículo",
        [ReglasLeads.ResponsableAdministracion] = "Administración",
        ["ASESOR"] = "Asesor"
    };

    /// <summary>Nombre del código en el diccionario; si no está, el código tal cual (nunca null salvo que lo sea).</summary>
    public static string? De(IReadOnlyDictionary<string, string> d, string? codigo) =>
        codigo is null ? null : d.TryGetValue(codigo, out var n) ? n : codigo;

    /// <summary>"$ 95.000.000" (pesos enteros, separador de miles de Colombia).</summary>
    public static string Pesos(long valor) => "$ " + valor.ToString("N0", Colombia);

    public static string Fecha(DateOnly dia) => dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string FechaUtc(DateTime utc) => Fecha(FiltrosReporte.DiaColombia(utc));

    public static string Porcentaje(double? valor) => valor is null ? "Sin datos" : valor.Value.ToString("0.0", Colombia);
}
