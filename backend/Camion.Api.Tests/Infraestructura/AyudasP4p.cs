using System.Net;
using System.Text.Json;
using Camion.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Camion.Api.Tests.Infraestructura;

/// <summary>
/// P4p: llamadas y datos de prueba de planes, suscripciones y catálogos. Usa <see cref="InventarioApi"/> con un
/// <see cref="RelojFijo"/> (SQLite en memoria; nunca MariaDB). Ningún dato es real.
/// </summary>
public static class AyudasP4p
{
    public static readonly DateTime T = CatalogoApi.T;

    public const string MsgRevisar = "Revisa los datos marcados.";
    public const string MsgFiltros = "Revisa los filtros.";
    public const string MsgNoEncontrado = "No encontrado.";
    public const string MsgSinPermiso = "No tienes permiso para realizar esta acción.";
    public const string MsgPlanDuplicado = "Ya existe un plan con ese nombre.";
    public const string MsgPlan = "Elige un plan.";
    public const string MsgPlanActivo = "Elige un plan activo.";
    public const string MsgYaTieneViva =
        "Este asesor ya tiene una suscripción activa, reactivada o suspendida. Renuévala o cámbiale el plan.";
    public const string MsgAsesorNoActivo = "Solo se puede asignar un plan a un asesor con la cuenta activa.";
    public const string MsgSinSuscripcion = "Aún no tienes un plan activo. Comunícate con el administrador.";
    public const string MsgMotivo = "Escribe el motivo (5 a 500 caracteres).";
    public const string MotivoValido = "Pago pendiente (prueba)";

    public static RelojFijo NuevoReloj() => new(new DateTimeOffset(T));

    public static void Hora(this InventarioApi api, DateTime utc) => api.Reloj!.Ahora = new DateTimeOffset(utc);

    public static string Unico(string prefijo) => $"{prefijo} {Guid.NewGuid().ToString("N")[..8]}";

    public static Dictionary<string, object?> CuerpoPlan(string? nombre = null, object? precio = null, object? meses = null,
        object? limite = null, bool? esPrueba = false) => new()
    {
        ["nombre"] = nombre ?? Unico("Plan"),
        ["precioCop"] = precio ?? 100_000,
        ["duracionMeses"] = meses ?? 1,
        ["limitePublicaciones"] = limite ?? 5,
        ["esPrueba"] = esPrueba
    };

    public static async Task<JsonElement> Esperar(HttpResponseMessage r, HttpStatusCode codigo)
    {
        var texto = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == codigo, $"Se esperaba {(int)codigo} y llegó {(int)r.StatusCode}: {texto}");
        return texto.Length == 0 ? default : JsonDocument.Parse(texto).RootElement.Clone();
    }

    public static async Task Error(HttpResponseMessage r, HttpStatusCode codigo, string mensaje) =>
        Assert.Equal(mensaje, (await Esperar(r, codigo)).GetProperty("error").GetString());

    /// <summary>400 { error, errores: { campo: [mensaje] } }.</summary>
    public static async Task Error400(HttpResponseMessage r, string campo, string mensaje, string error = MsgRevisar)
    {
        var json = await Esperar(r, HttpStatusCode.BadRequest);
        Assert.Equal(error, json.GetProperty("error").GetString());
        Assert.True(json.GetProperty("errores").TryGetProperty(campo, out var lista), $"Falta errores.{campo}: {json}");
        Assert.Equal(new[] { mensaje }, lista.EnumerateArray().Select(x => x.GetString()).ToArray());
    }

    // ---------------------------------------------------------------- planes

    public static async Task<JsonElement> CrearPlan(this InventarioApi api, string? nombre = null, int meses = 1,
        int limite = 5, long precio = 100_000) =>
        await Esperar(await api.Enviar(HttpMethod.Post, "/api/admin/planes", api.Admin.Token,
            CuerpoPlan(nombre, precio, meses, limite)), HttpStatusCode.Created);

    public static async Task<long> PlanId(this InventarioApi api, int meses = 1, int limite = 5) =>
        (await api.CrearPlan(meses: meses, limite: limite)).GetProperty("id").GetInt64();

    // ---------------------------------------------------------------- suscripciones

    public static Task<HttpResponseMessage> Asignar(this InventarioApi api, long asesorId, long? planId, string? token = "") =>
        api.Enviar(HttpMethod.Post, $"/api/admin/asesores/{asesorId}/suscripcion", token == "" ? api.Admin.Token : token,
            new { planId });

    public static async Task<JsonElement> Asignada(this InventarioApi api, long asesorId, long planId) =>
        await Esperar(await api.Asignar(asesorId, planId), HttpStatusCode.Created);

    public static Task<HttpResponseMessage> AccionSus(this InventarioApi api, long id, string accion, object? cuerpo = null,
        string? token = "") =>
        api.Enviar(HttpMethod.Post, $"/api/admin/suscripciones/{id}/{accion}", token == "" ? api.Admin.Token : token,
            cuerpo ?? new { });

    public static async Task<JsonElement> AccionOk(this InventarioApi api, long id, string accion, object? cuerpo = null) =>
        await Esperar(await api.AccionSus(id, accion, cuerpo), HttpStatusCode.OK);

    public static Task<HttpResponseMessage> MiSuscripcion(this InventarioApi api, string? token) =>
        api.Enviar(HttpMethod.Get, "/api/asesor/suscripcion", token);

    public static Task<Suscripcion> SusDb(this InventarioApi api, long id) =>
        api.Leer(db => db.Suscripciones.AsNoTracking().SingleAsync(s => s.Id == id));

    public static Task<List<AuditoriaEvento>> Auditoria(this InventarioApi api, string entidad, string entidadId) =>
        api.Leer(db => db.AuditoriaEventos.AsNoTracking().Where(a => a.Entidad == entidad && a.EntidadId == entidadId)
            .OrderBy(a => a.Id).ToListAsync());

    public static DateTime Fecha(JsonElement e, string campo) =>
        e.GetProperty(campo).GetDateTime().ToUniversalTime();

    public static Dictionary<string, JsonElement> JsonDe(string? texto) =>
        texto is null ? new() : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(texto)!;

    // ---------------------------------------------------------------- catálogos

    public static Task<HttpResponseMessage> Catalogo(this InventarioApi api, string catalogo, string? token = "") =>
        api.Enviar(HttpMethod.Get, $"/api/admin/catalogos/{catalogo}", token == "" ? api.Admin.Token : token);

    public static Task<HttpResponseMessage> CrearElemento(this InventarioApi api, string catalogo, string? nombre,
        string? token = "") =>
        api.Enviar(HttpMethod.Post, $"/api/admin/catalogos/{catalogo}", token == "" ? api.Admin.Token : token, new { nombre });

    public static Task<HttpResponseMessage> Renombrar(this InventarioApi api, string catalogo, long id, string? nombre) =>
        api.Enviar(HttpMethod.Put, $"/api/admin/catalogos/{catalogo}/{id}", api.Admin.Token, new { nombre });

    public static Task<HttpResponseMessage> ActivarElemento(this InventarioApi api, string catalogo, long id, bool activo) =>
        api.Enviar(HttpMethod.Post, $"/api/admin/catalogos/{catalogo}/{id}/{(activo ? "activar" : "desactivar")}",
            api.Admin.Token);

    public static Task<HttpResponseMessage> CrearLinea(this InventarioApi api, long marcaId, string? nombre, long[]? tipoIds) =>
        api.Enviar(HttpMethod.Post, $"/api/admin/catalogos/marcas/{marcaId}/lineas", api.Admin.Token, new { nombre, tipoIds });

    public static Task<HttpResponseMessage> EditarLinea(this InventarioApi api, long id, object cuerpo) =>
        api.Enviar(HttpMethod.Put, $"/api/admin/catalogos/lineas/{id}", api.Admin.Token, cuerpo);
}

/// <summary>Fixture por clase: API de inventario sembrada con un reloj fijo en T. Cada prueba fija la hora que necesita.</summary>
public sealed class P4pFixture : IAsyncLifetime
{
    public InventarioApi Api { get; private set; } = null!;
    public async Task InitializeAsync() => Api = await InventarioApi.Crear(AyudasP4p.NuevoReloj());
    public Task DisposeAsync()
    {
        Api.Dispose();
        return Task.CompletedTask;
    }
}
