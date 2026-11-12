using System.Net;
using Camion.Api.Models;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP5;

namespace Camion.Api.Tests.Infraestructura;

/// <summary>
/// P5b: llamadas y datos de prueba de los avisos "Vende tu camión" (HU-N01) y "Compra tu seguro todo riesgo" (HU-N02).
/// Usa <see cref="InventarioApi"/> (SQLite en memoria; nunca MariaDB). Ningún dato es real.
/// </summary>
public static class AyudasP5b
{
    public const string RutaVende = "/api/avisos/vende-tu-camion";
    public const string RutaSeguro = "/api/avisos/seguro-todo-riesgo";

    public const string MsgExitoAviso = "¡Listo! Recibimos tu solicitud. Te contactaremos pronto por teléfono o WhatsApp.";
    public const string MsgDescripcionAviso = "Describe tu vehículo (máximo 500 caracteres).";
    public const string MsgDescripcionLarga = "La descripción no puede pasar de 500 caracteres.";
    public const string MsgTipoVehiculo = "Elige el tipo de vehículo.";
    public const string MsgSeguro = "Solicitud de seguro todo riesgo";
    public const string MsgOrigen = "Origen no válido.";
    public const string MsgNoAplica = "Ese resultado no aplica a esta solicitud.";
    public const string MsgAvisoAdmin = "Esta solicitud la atiende siempre la administración.";
    public const string DescripcionPrueba = "Kenworth T800 modelo 2015, 450.000 km, en Medellín (prueba)";

    public static Dictionary<string, object?> CuerpoVende(string? telefono = null, string? descripcion = DescripcionPrueba) => new()
    {
        ["nombre"] = "Vendedor Ficticio",
        ["telefono"] = telefono ?? TelefonoUnico(),
        ["correo"] = null,
        ["descripcion"] = descripcion,
        ["autorizacionDatos"] = true
    };

    public static Dictionary<string, object?> CuerpoSeguro(string? telefono = null, string? tipo = "CAMION") => new()
    {
        ["nombre"] = "Asegurado Ficticio",
        ["telefono"] = telefono ?? TelefonoUnico(),
        ["correo"] = null,
        ["tipoVehiculo"] = tipo,
        ["autorizacionDatos"] = true
    };

    public static Task<HttpResponseMessage> Aviso(this InventarioApi api, string ruta, object cuerpo, string? token = null) =>
        api.Enviar(HttpMethod.Post, ruta, token, cuerpo);

    /// <summary>201 con solo { mensaje } y el texto de éxito de los avisos.</summary>
    public static async Task AvisoOk(this InventarioApi api, string ruta, object cuerpo, string? token = null)
    {
        var json = await Esperar(await api.Aviso(ruta, cuerpo, token), HttpStatusCode.Created);
        Assert.Equal(MsgExitoAviso, json.GetProperty("mensaje").GetString());
        Assert.Equal(new[] { "mensaje" }, json.EnumerateObject().Select(p => p.Name).ToArray());
    }

    /// <summary>Envía el aviso y devuelve el lead más reciente de ese teléfono (10 dígitos sin +57).</summary>
    public static async Task<Lead> AvisoNuevo(this InventarioApi api, string ruta, string? telefono = null, string? token = null)
    {
        telefono ??= TelefonoUnico();
        await api.AvisoOk(ruta, ruta == RutaVende ? CuerpoVende(telefono) : CuerpoSeguro(telefono), token);
        return await api.LeadDeTelefono(telefono);
    }

    public static Task<Lead> LeadDeTelefono(this InventarioApi api, string telefono) =>
        api.Leer(db => db.Leads.AsNoTracking().Where(l => l.Telefono == "+57" + telefono).OrderByDescending(l => l.Id).FirstAsync());

    public static Task<int> LeadsDeTelefono(this InventarioApi api, string telefono) =>
        api.Leer(db => db.Leads.CountAsync(l => l.Telefono == "+57" + telefono));
}
