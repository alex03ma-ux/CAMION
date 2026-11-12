using System.Net;
using System.Text.Json;
using Camion.Api.Models;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP5;

namespace Camion.Api.Tests.Infraestructura;

/// <summary>
/// P6: llamadas y datos de prueba de créditos (HU-039), búsquedas (HU-034), vehículos relacionados (HU-036) y "Mis
/// solicitudes" (D-53, D-59). Usa <see cref="InventarioApi"/> (SQLite en memoria; nunca MariaDB). Ningún dato es real.
/// </summary>
public static class AyudasP6
{
    public const string RutaCredito = "/api/creditos";
    public const string RutaBusqueda = "/api/busquedas";
    public const string RutaMias = "/api/mis-solicitudes";

    public const string MsgExitoSolicitud = "¡Listo! Recibimos tu solicitud. Te contactaremos pronto por teléfono o WhatsApp.";
    public const string MsgCredito = "Solicitud de crédito";
    public const string MsgCreditoVehiculo = "Solicitud de crédito para este vehículo";
    public const string MsgComentarioLargo = "El comentario no puede pasar de 500 caracteres.";
    public const string MsgBusca = "Busco un vehículo";
    public const string MsgCategoria = "Elige la categoría del vehículo que buscas.";
    public const string MsgMarca = "Elige una marca de la lista.";
    public const string MsgModelo = "Elige un modelo de la lista.";
    public const string MsgAnios = "Revisa el rango de años.";
    public const string MsgPresupuesto = "Escribe un valor en pesos entre 1.000.000 y 5.000.000.000, o deja el campo vacío.";
    public const string MsgDepartamento = "Elige un departamento de la lista.";
    public const string MsgCiudad = "Elige una ciudad de la lista.";
    public const string MsgSoloBusqueda = "Solo se pueden relacionar vehículos a una búsqueda.";
    public const string MsgBusquedaCerrada = "Esta búsqueda está cerrada.";
    public const string MsgNoPublicado = "Ese vehículo no está publicado.";
    public const string MsgYaRelacionado = "Ese vehículo ya está relacionado con esta búsqueda.";
    public const string MsgTope = "Esta búsqueda ya tiene 10 vehículos relacionados.";
    public const string MsgNoRelacionado = "Ese vehículo no está relacionado con esta búsqueda.";
    public const string MsgElegirVehiculo = "Elige un vehículo.";
    public const string MsgNotaRelacion = "La nota no puede pasar de 300 caracteres.";
    public const string MsgSolicitudNoEncontrada = "Solicitud no encontrada.";
    public const string MsgFiltrosP6 = "Revisa los filtros.";
    public const string MsgPaginaP6 = "Página o tamaño no válidos.";
    public const string NotaP6 = "Llamé al cliente y quedó de enviar fotos (prueba)";

    public static Dictionary<string, object?> CuerpoCredito(string? slug = null, string? telefono = null, string? mensaje = null) => new()
    {
        ["slug"] = slug,
        ["nombre"] = "Solicitante Ficticio",
        ["telefono"] = telefono ?? TelefonoUnico(),
        ["correo"] = null,
        ["mensaje"] = mensaje,
        ["autorizacionDatos"] = true
    };

    public static Dictionary<string, object?> CuerpoBusqueda(long tipoId, string? telefono = null) => new()
    {
        ["nombre"] = "Buscador Ficticio",
        ["telefono"] = telefono ?? TelefonoUnico(),
        ["correo"] = null,
        ["tipoId"] = tipoId,
        ["autorizacionDatos"] = true
    };

    public static Task<HttpResponseMessage> Solicitar(this InventarioApi api, string ruta, object cuerpo, string? token = null) =>
        api.Enviar(HttpMethod.Post, ruta, token, cuerpo);

    /// <summary>201 con solo { mensaje } y el texto de éxito.</summary>
    public static async Task SolicitudOk(this InventarioApi api, string ruta, object cuerpo, string? token = null)
    {
        var json = await Esperar(await api.Solicitar(ruta, cuerpo, token), HttpStatusCode.Created);
        Assert.Equal(MsgExitoSolicitud, json.GetProperty("mensaje").GetString());
        Assert.Equal(new[] { "mensaje" }, json.EnumerateObject().Select(p => p.Name).ToArray());
    }

    /// <summary>Envía un crédito (portada si slug es null) y devuelve el lead más reciente de ese teléfono.</summary>
    public static async Task<Lead> CreditoNuevo(this InventarioApi api, string? slug = null, string? token = null,
        string? telefono = null)
    {
        telefono ??= TelefonoUnico();
        await api.SolicitudOk(RutaCredito, CuerpoCredito(slug, telefono), token);
        return await AyudasP5b.LeadDeTelefono(api, telefono);
    }

    /// <summary>Envía una búsqueda con la categoría y los cambios dados; devuelve el lead.</summary>
    public static async Task<Lead> BusquedaNueva(this InventarioApi api, long tipoId, string? token = null,
        string? telefono = null, params (string Campo, object? Valor)[] cambios)
    {
        telefono ??= TelefonoUnico();
        await api.SolicitudOk(RutaBusqueda, InventarioApi.Con(CuerpoBusqueda(tipoId, telefono), cambios), token);
        return await AyudasP5b.LeadDeTelefono(api, telefono);
    }

    public static Task<LeadBusqueda?> CriteriosDb(this InventarioApi api, long leadId) =>
        api.Leer(db => db.LeadsBusqueda.AsNoTracking().SingleOrDefaultAsync(b => b.LeadId == leadId));

    public static Task<List<BusquedaVehiculo>> RelacionesDb(this InventarioApi api, long leadId) =>
        api.Leer(db => db.BusquedaVehiculos.AsNoTracking().Where(r => r.LeadId == leadId).OrderBy(r => r.Id).ToListAsync());

    public static Task<HttpResponseMessage> Relacionar(this InventarioApi api, string? token, long leadId, long? publicacionId,
        string? nota = null) =>
        api.Enviar(HttpMethod.Post, $"/api/leads/{leadId}/vehiculos", token, new { publicacionId, nota });

    public static Task<HttpResponseMessage> Retirar(this InventarioApi api, string? token, long leadId, long publicacionId) =>
        api.Enviar(HttpMethod.Post, $"/api/leads/{leadId}/vehiculos/{publicacionId}/retirar", token, new { });

    public static Task<HttpResponseMessage> Coincidencias(this InventarioApi api, string? token, long leadId) =>
        api.Enviar(HttpMethod.Get, $"/api/leads/{leadId}/coincidencias", token);

    public static async Task<long[]> IdsCoincidencias(this InventarioApi api, long leadId) =>
        (await Esperar(await api.Coincidencias(api.Admin.Token, leadId), HttpStatusCode.OK)).GetProperty("items")
        .EnumerateArray().Select(x => x.GetProperty("publicacionId").GetInt64()).ToArray();

    public static async Task<JsonElement> DetalleAdmin(this InventarioApi api, long leadId) =>
        await Esperar(await api.DetalleLead(api.Admin.Token, leadId), HttpStatusCode.OK);

    public static Task<HttpResponseMessage> Nota(this InventarioApi api, string? token, long leadId, string texto = NotaP6) =>
        api.AccionLead(token, leadId, "notas", new { texto });

    public static Task<HttpResponseMessage> Cerrar(this InventarioApi api, string? token, long leadId, string resultado,
        string? observacion = null) =>
        api.AccionLead(token, leadId, "cerrar", new { resultado, observacion });

    public static Task<HttpResponseMessage> Mias(this InventarioApi api, string? token, string query = "") =>
        api.Enviar(HttpMethod.Get, RutaMias + query, token);

    public static async Task<JsonElement[]> ItemsMias(this InventarioApi api, string token, string query = "") =>
        (await Esperar(await api.Mias(token, query), HttpStatusCode.OK)).GetProperty("items").EnumerateArray().ToArray();

    /// <summary>Comprador nuevo (registro público, D-3): su token y su id.</summary>
    public static async Task<(string Token, string Id)> CompradorNuevo(this InventarioApi api)
    {
        var email = Ayudas.CorreoUnico();
        var token = await Ayudas.RegistrarYObtenerToken(api.C, email);
        var id = await api.Leer(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());
        return (token, id);
    }

    /// <summary>Todo lo guardado sobre un lead (fila, criterios, eventos, consentimientos y auditoría) como texto.</summary>
    public static async Task<string> TodoLoGuardado(this InventarioApi api, long leadId)
    {
        var lead = await api.LeadDb(leadId);
        var criterios = await api.CriteriosDb(leadId);
        var eventos = await api.EventosDb(leadId);
        var consentimientos = await api.ConsentimientosDb(leadId);
        var auditoria = await api.AuditoriaLead(leadId);
        return JsonSerializer.Serialize(new { lead, criterios, eventos, consentimientos, auditoria });
    }
}
