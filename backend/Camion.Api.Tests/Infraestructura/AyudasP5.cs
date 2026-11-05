using System.Net;
using System.Text.Json;
using Camion.Api.Models;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4b;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;

namespace Camion.Api.Tests.Infraestructura;

/// <summary>
/// P5: llamadas y datos de prueba de contactos y leads. Usa <see cref="InventarioApi"/> con un <see cref="RelojFijo"/> en T
/// (SQLite en memoria; nunca MariaDB). Ningún dato es real: nombres y teléfonos son ficticios.
/// </summary>
public static class AyudasP5
{
    public const string MsgExito = "¡Listo! Recibimos tus datos. Te contactaremos pronto por teléfono o WhatsApp.";
    public const string MsgNombre = "Escribe tu nombre (entre 2 y 150 caracteres).";
    public const string MsgTelefono = "Escribe un celular o fijo colombiano de 10 dígitos, por ejemplo 300 123 4567.";
    public const string MsgCorreo = "Escribe un correo válido o deja el campo vacío.";
    public const string MsgMensaje = "El mensaje no puede pasar de 500 caracteres.";
    public const string MsgAutorizacion = "Debes autorizar el tratamiento de tus datos para enviar el formulario.";
    public const string MsgSlug = "Indica el vehículo que quieres contactar.";
    public const string MsgNoDisponible = "Este vehículo ya no está disponible. Mira otros vehículos en el catálogo.";
    public const string MsgPropio = "Este vehículo es tuyo; no puedes contactarte a ti mismo.";
    public const string MsgGrande = "La solicitud es demasiado grande.";
    public const string MsgMuchas = "Has enviado muchas solicitudes. Intenta de nuevo en unos minutos.";
    public const string MsgLeadNoEncontrado = "Contacto no encontrado.";
    public const string MsgNota = "La nota debe tener entre 5 y 500 caracteres.";
    public const string MsgCerrado = "Este contacto está cerrado.";
    public const string MsgYaCerrado = "Este contacto ya está cerrado.";
    public const string MsgYaAbierto = "Este contacto ya está abierto.";
    public const string MsgResultado = "Elige el resultado del cierre.";
    public const string MsgObservacionOtro = "Escribe la observación (5 a 500 caracteres).";
    public const string MsgAsesorAprobado = "Elige un asesor aprobado.";
    public const string MsgMismoResponsable = "El contacto ya está asignado a ese responsable.";
    public const string VersionPolitica = "BORRADOR-2026-11-04";
    public const string MotivoP5 = "Lo atiende otra persona (prueba)";

    /// <summary>Celular ficticio único (3 + 9 dígitos) para que los duplicados y el límite por teléfono no se crucen.</summary>
    public static string TelefonoUnico() => "3" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    public static Dictionary<string, object?> CuerpoContacto(string? slug, string? telefono = null) => new()
    {
        ["slug"] = slug,
        ["nombre"] = "Visitante Ficticio",
        ["telefono"] = telefono ?? TelefonoUnico(),
        ["correo"] = null,
        ["mensaje"] = null,
        ["autorizacionDatos"] = true
    };

    public static Task<HttpResponseMessage> Contactar(this InventarioApi api, object cuerpo, string? token = null) =>
        api.Enviar(HttpMethod.Post, "/api/contactos", token, cuerpo);

    public static async Task ContactoOk(this InventarioApi api, object cuerpo, string? token = null)
    {
        var json = await Esperar(await api.Contactar(cuerpo, token), HttpStatusCode.Created);
        Assert.Equal(MsgExito, json.GetProperty("mensaje").GetString());
        Assert.Equal(new[] { "mensaje" }, json.EnumerateObject().Select(p => p.Name).ToArray());
    }

    /// <summary>Asesor nuevo con plan en T y una publicación PUBLICADA (vehículo con foto). Devuelve el asesor y la publicación.</summary>
    public static async Task<(AsesorPrueba Asesor, JsonElement Pub, string Slug)> AsesorPublicado(this InventarioApi api, string nombre)
    {
        var (asesor, _) = await api.AsesorConPlan(nombre);
        var pub = await api.Publicada(asesor);
        return (asesor, pub, AyudasP4b.Slug(pub));
    }

    /// <summary>Crea un lead (contacto anónimo) y devuelve su fila.</summary>
    public static async Task<Lead> LeadNuevo(this InventarioApi api, string slug, string? telefono = null, string? token = null)
    {
        telefono ??= TelefonoUnico();
        await api.ContactoOk(CuerpoContacto(slug, telefono), token);
        return await api.Leer(db => db.Leads.AsNoTracking().Where(l => l.Publicacion!.SlugPublico == slug)
            .OrderByDescending(l => l.Id).FirstAsync());
    }

    public static Task<Lead> LeadDb(this InventarioApi api, long id) =>
        api.Leer(db => db.Leads.AsNoTracking().SingleAsync(l => l.Id == id));

    public static Task<List<EventoLead>> EventosDb(this InventarioApi api, long leadId) =>
        api.Leer(db => db.EventosLead.AsNoTracking().Where(e => e.LeadId == leadId).OrderBy(e => e.Id).ToListAsync());

    public static Task<List<ConsentimientoDatos>> ConsentimientosDb(this InventarioApi api, long leadId) =>
        api.Leer(db => db.ConsentimientosDatos.AsNoTracking()
            .Where(c => c.Entidad == "Lead" && c.EntidadId == leadId.ToString()).OrderBy(c => c.Id).ToListAsync());

    public static Task<int> TotalLeads(this InventarioApi api) => api.Leer(db => db.Leads.CountAsync());

    public static Task<HttpResponseMessage> ListaLeads(this InventarioApi api, string? token, string query = "") =>
        api.Enviar(HttpMethod.Get, "/api/leads" + query, token);

    public static async Task<long[]> IdsLeads(this InventarioApi api, string? token, string query = "")
    {
        var json = await Esperar(await api.ListaLeads(token, query), HttpStatusCode.OK);
        return json.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToArray();
    }

    public static Task<HttpResponseMessage> DetalleLead(this InventarioApi api, string? token, long id) =>
        api.Enviar(HttpMethod.Get, $"/api/leads/{id}", token);

    public static async Task<JsonElement> Resumen(this InventarioApi api, string? token) =>
        await Esperar(await api.Enviar(HttpMethod.Get, "/api/leads/resumen", token), HttpStatusCode.OK);

    public static Task<HttpResponseMessage> AccionLead(this InventarioApi api, string? token, long id, string accion, object? cuerpo) =>
        api.Enviar(HttpMethod.Post, $"/api/leads/{id}/{accion}", token, cuerpo ?? new { });

    public static Task<HttpResponseMessage> ReasignarLead(this InventarioApi api, string? token, long id, long? asesorId,
        bool? administracion = null, string? motivo = MotivoP5) =>
        api.AccionLead(token, id, "reasignar", new { asesorId, administracion, motivo });

    public static Task<List<AuditoriaEvento>> AuditoriaLead(this InventarioApi api, long id) =>
        api.Auditoria("Lead", id.ToString());
}

/// <summary>Fixture por clase para P5: API de inventario sembrada con un reloj fijo en T.</summary>
public sealed class P5Fixture : IAsyncLifetime
{
    public InventarioApi Api { get; private set; } = null!;
    public async Task InitializeAsync() => Api = await InventarioApi.Crear(AyudasP4p.NuevoReloj());
    public Task DisposeAsync()
    {
        Api.Dispose();
        return Task.CompletedTask;
    }
}
