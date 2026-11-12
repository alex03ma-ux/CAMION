using System.Net;
using System.Text.Json;
using Camion.Api.Models;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;

namespace Camion.Api.Tests.Infraestructura;

/// <summary>
/// P7: llamadas y datos de prueba de ventas, pagos, parámetros y panel (docs/criterios/P7-contrato-api.md).
/// Usa <see cref="InventarioApi"/> con un <see cref="RelojFijo"/> en T (SQLite en memoria; nunca MariaDB). Ningún dato es real.
/// </summary>
public static class AyudasP7
{
    public const string MsgFecha = "Escribe una fecha válida (aaaa-mm-dd).";
    public const string MsgRango = "La fecha inicial no puede ser posterior a la final.";
    public const string MsgOrigen = "Origen no válido.";
    public const string MsgEstado = "Estado no válido.";
    public const string MsgAsesor = "Asesor no válido.";
    public const string MsgPagina = "Página o tamaño no válidos.";

    public const string MsgValor = "Escribe el valor pagado en pesos (número entero mayor que 0).";
    public const string MsgFechaPago = "Escribe la fecha del pago (AAAA-MM-DD), de hoy o anterior.";
    public const string MsgMedio = "Elige el medio de pago.";
    public const string MsgReferencia = "La referencia no puede tener más de 100 caracteres ni los signos < >.";
    public const string MsgReferenciaOtro = "Escribe la referencia: es obligatoria si el medio es Otro.";
    public const string MsgPlanSinPrecio = "Este plan no tiene precio: no se registra un pago.";
    public const string MsgPagoNoEncontrado = "Pago no encontrado.";
    public const string MsgYaAnulado = "Este pago ya está anulado.";

    public const string MsgWhatsapp = "Escribe un celular colombiano de 10 dígitos, por ejemplo 316 615 2907.";
    public const string MsgCorreo = "Escribe un correo válido.";
    public const string MsgHorario = "Escribe de 1 a 4 líneas de horario, de hasta 100 caracteres cada una y sin los signos < >.";
    public const string MsgInstagram = "Escribe el enlace de Instagram (https://www.instagram.com/...) o deja el campo vacío.";
    public const string MsgTiktok = "Escribe el enlace de TikTok (https://www.tiktok.com/@...) o deja el campo vacío.";

    public static readonly string[] HorarioD42 =
        ["Lunes a viernes: 9:00 a. m. a 5:00 p. m.", "Sábados: 9:00 a. m. a 3:00 p. m."];

    // ---------------------------------------------------------------- ventas

    public static Task<HttpResponseMessage> Ventas(this InventarioApi api, string? token, string query = "") =>
        api.Enviar(HttpMethod.Get, "/api/ventas" + query, token);

    public static async Task<JsonElement> VentasOk(this InventarioApi api, string? token, string query = "") =>
        await Esperar(await api.Ventas(token, query), HttpStatusCode.OK);

    public static Task<HttpResponseMessage> VentasVehiculo(this InventarioApi api, string? token, long vehiculoId) =>
        api.Enviar(HttpMethod.Get, $"/api/inventario/vehiculos/{vehiculoId}/ventas", token);

    public static long[] Ids(JsonElement lista) =>
        lista.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToArray();

    /// <summary>Vehículo del asesor vendido hoy (sin publicación). Devuelve el id del vehículo.</summary>
    public static async Task<long> VehiculoVendido(this InventarioApi api, AsesorPrueba asesor, string origen = "PORTAL",
        long precio = 95_000_000, string? observaciones = null)
    {
        var id = await api.CrearId(asesor);
        await Esperar(await api.Vender(asesor.Token, id, new { fechaVenta = AyudasP4b.HoyT, precioFinalCop = precio, origen, observaciones }),
            HttpStatusCode.OK);
        return id;
    }

    // ---------------------------------------------------------------- pagos

    public static object PagoValido(object? valorCop = null, string? fechaPago = null, string medio = "TRANSFERENCIA",
        string? referencia = "TRX-PRUEBA-1") => new { valorCop, fechaPago = fechaPago ?? AyudasP4b.HoyT, medio, referencia };

    public static Task<HttpResponseMessage> AsignarConPago(this InventarioApi api, long asesorId, long planId, object? pago,
        string? token = "") =>
        api.Enviar(HttpMethod.Post, $"/api/admin/asesores/{asesorId}/suscripcion", token == "" ? api.Admin.Token : token,
            new { planId, pago });

    public static Task<HttpResponseMessage> Pagos(this InventarioApi api, long suscripcionId, string? token = "") =>
        api.Enviar(HttpMethod.Get, $"/api/admin/suscripciones/{suscripcionId}/pagos", token == "" ? api.Admin.Token : token);

    public static async Task<JsonElement> PagosOk(this InventarioApi api, long suscripcionId) =>
        await Esperar(await api.Pagos(suscripcionId), HttpStatusCode.OK);

    public static Task<HttpResponseMessage> RegistrarPago(this InventarioApi api, long suscripcionId, object? cuerpo,
        string? token = "") =>
        api.Enviar(HttpMethod.Post, $"/api/admin/suscripciones/{suscripcionId}/pagos", token == "" ? api.Admin.Token : token, cuerpo);

    public static Task<HttpResponseMessage> AnularPago(this InventarioApi api, long pagoId, string? motivo = "Valor mal escrito (prueba)",
        string? token = "") =>
        api.Enviar(HttpMethod.Post, $"/api/admin/pagos/{pagoId}/anular", token == "" ? api.Admin.Token : token, new { motivo });

    public static Task<List<Pago>> PagosDb(this InventarioApi api, long suscripcionId) =>
        api.Leer(db => db.Pagos.AsNoTracking().Where(p => p.SuscripcionId == suscripcionId).OrderBy(p => p.Id).ToListAsync());

    public static Task<int> TotalPagos(this InventarioApi api) => api.Leer(db => db.Pagos.CountAsync());

    public static Task<int> TotalSuscripciones(this InventarioApi api, long asesorId) =>
        api.Leer(db => db.Suscripciones.CountAsync(s => s.AsesorId == asesorId));

    /// <summary>Plan nuevo con el precio dado (0 = sin precio).</summary>
    public static async Task<long> PlanConPrecio(this InventarioApi api, long precio, int meses = 1) =>
        (await api.CrearPlan(meses: meses, precio: precio)).GetProperty("id").GetInt64();

    // ---------------------------------------------------------------- parámetros

    public static Dictionary<string, object?> CuerpoContacto(string? whatsapp = "317 000 0001", string? correo = "contacto@prueba.test",
        object? horario = null, string? instagram = null, string? tiktok = null) => new()
    {
        ["whatsapp"] = whatsapp,
        ["correo"] = correo,
        ["horario"] = horario ?? new[] { "Lunes a sábado: 8:00 a. m. a 6:00 p. m." },
        ["instagram"] = instagram,
        ["tiktok"] = tiktok
    };

    public static Task<HttpResponseMessage> GuardarContacto(this InventarioApi api, object cuerpo, string? token = "") =>
        api.Enviar(HttpMethod.Put, "/api/admin/parametros/contacto", token == "" ? api.Admin.Token : token, cuerpo);

    public static async Task<JsonElement> ContactoPublico(this InventarioApi api) =>
        await Esperar(await api.Enviar(HttpMethod.Get, "/api/portal/contacto", null), HttpStatusCode.OK);

    public static Task<HttpResponseMessage> Panel(this InventarioApi api, string? token = "") =>
        api.Enviar(HttpMethod.Get, "/api/admin/panel", token == "" ? api.Admin.Token : token);

    public static string[] Lista(JsonElement e, string campo) =>
        e.GetProperty(campo).EnumerateArray().Select(x => x.GetString()!).ToArray();
}

/// <summary>Fixture por clase para P7: API de inventario sembrada con un reloj fijo en T.</summary>
public sealed class P7Fixture : IAsyncLifetime
{
    public InventarioApi Api { get; private set; } = null!;
    public async Task InitializeAsync() => Api = await InventarioApi.Crear(AyudasP4p.NuevoReloj());
    public Task DisposeAsync()
    {
        Api.Dispose();
        return Task.CompletedTask;
    }
}
