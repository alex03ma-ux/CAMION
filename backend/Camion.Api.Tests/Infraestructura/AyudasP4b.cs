using System.Net;
using System.Text.Json;
using Camion.Api.Models;
using Microsoft.EntityFrameworkCore;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;

namespace Camion.Api.Tests.Infraestructura;

/// <summary>
/// P4b: llamadas y datos de prueba de publicaciones, destacados, venta y reasignación. Usa <see cref="InventarioApi"/>
/// con un <see cref="RelojFijo"/> en T (SQLite en memoria; nunca MariaDB). Ningún dato es real.
/// </summary>
public static class AyudasP4b
{
    public const string MsgSinPlan = "No tienes un plan activo. Comunícate con el administrador.";
    public const string MsgNoEncontrada = "Publicación no encontrada.";
    public const string MsgVehiculoNoEncontrado = "Vehículo no encontrado.";
    public const string MsgYaPublicado = "Este vehículo ya tiene una publicación.";
    public const string MsgSinFoto = "Agrega al menos una foto del vehículo antes de publicarlo.";
    public const string MsgSinPrecio = "Escribe el precio del vehículo antes de publicarlo.";
    public const string MsgTitulo = "Escribe el título (5 a 180 caracteres).";
    public const string MsgDescripcion = "La descripción no puede tener más de 4000 caracteres.";
    public const string MsgVehiculoId = "Elige un vehículo.";
    public const string MsgMismoAsesor = "El vehículo ya pertenece a ese asesor.";
    public const string MsgDestinoSinPlan =
        "El asesor destino no tiene un plan activo: no tiene cupo para la publicación de este vehículo.";
    public const string MsgAsesorActivo = "Elige un asesor activo.";
    public const string MsgLimiteDestacados = "Escribe los destacados incluidos (número entero de 0 a 20).";
    public const string MotivoInactivar = "Lo retiro por unos días (prueba)";

    public static string MsgCupo(int n) => $"Ya usas las {n} publicaciones de tu plan. Elimina una para crear otra.";
    public static string MsgDestacados(int n) => $"Tu plan permite {n} destacado(s). Quita uno para destacar otra.";
    public static string MsgDestinoSinCupo(int n) => $"El asesor destino ya usa las {n} publicaciones de su plan.";
    public static string MsgTransicion(string accion, string estado) => $"No se puede {accion} una publicación en estado {estado}.";

    /// <summary>Hoy en Colombia (UTC-5) para el reloj en T: 2026-10-26.</summary>
    public static string HoyT => DateOnly.FromDateTime(T.AddHours(-5)).ToString("yyyy-MM-dd");

    // ---------------------------------------------------------------- planes y asesores con plan

    public static async Task<long> PlanP4b(this InventarioApi api, int limite = 5, int destacados = 1, int meses = 1)
    {
        var cuerpo = CuerpoPlan(limite: limite, meses: meses);
        cuerpo["limiteDestacados"] = destacados;
        var json = await Esperar(await api.Enviar(HttpMethod.Post, "/api/admin/planes", api.Admin.Token, cuerpo),
            HttpStatusCode.Created);
        return json.GetProperty("id").GetInt64();
    }

    /// <summary>Asesor nuevo (aprobado) con una suscripción ACTIVA asignada en T por 30 x meses días.</summary>
    public static async Task<(AsesorPrueba Asesor, long SusId)> AsesorConPlan(this InventarioApi api, string nombre,
        int limite = 5, int destacados = 1, int meses = 1)
    {
        api.Hora(T);
        var asesor = await api.CrearAsesor(nombre);
        var plan = await api.PlanP4b(limite, destacados, meses);
        var sus = (await api.Asignada(asesor.PerfilId, plan)).GetProperty("id").GetInt64();
        return (asesor, sus);
    }

    // ---------------------------------------------------------------- publicaciones

    public static Task<HttpResponseMessage> Publicar(this InventarioApi api, string? token, long? vehiculoId,
        string? titulo = "Camión de prueba listo", string? descripcion = null, bool? negociable = false) =>
        api.Enviar(HttpMethod.Post, "/api/publicaciones", token,
            new { vehiculoId, titulo, descripcionComercial = descripcion, negociable });

    /// <summary>Crea un vehículo (camión con una foto) del asesor y lo publica; devuelve la publicación (201).</summary>
    public static async Task<JsonElement> Publicada(this InventarioApi api, AsesorPrueba asesor, string titulo = "Camión de prueba listo")
    {
        var vehiculo = await api.CrearId(asesor);
        return await Esperar(await api.Publicar(asesor.Token, vehiculo, titulo), HttpStatusCode.Created);
    }

    public static Task<HttpResponseMessage> AccionPub(this InventarioApi api, string? token, long id, string accion,
        object? cuerpo = null) =>
        api.Enviar(HttpMethod.Post, $"/api/publicaciones/{id}/{accion}", token, cuerpo ?? new { });

    public static async Task<JsonElement> AccionPubOk(this InventarioApi api, string? token, long id, string accion,
        object? cuerpo = null) =>
        await Esperar(await api.AccionPub(token, id, accion, cuerpo), HttpStatusCode.OK);

    public static Task<JsonElement> Inactivada(this InventarioApi api, string token, long id) =>
        api.AccionPubOk(token, id, "inactivar", new { motivo = MotivoInactivar });

    public static Task<HttpResponseMessage> DetallePub(this InventarioApi api, string? token, long id) =>
        api.Enviar(HttpMethod.Get, $"/api/publicaciones/{id}", token);

    public static Task<HttpResponseMessage> ListaPub(this InventarioApi api, string? token, string query = "") =>
        api.Enviar(HttpMethod.Get, "/api/publicaciones" + query, token);

    public static async Task<long[]> IdsLista(this InventarioApi api, string? token, string query = "")
    {
        var json = await Esperar(await api.ListaPub(token, query), HttpStatusCode.OK);
        return json.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToArray();
    }

    public static async Task<JsonElement> Cupo(this InventarioApi api, string? token, long? asesorId = null) =>
        await Esperar(await api.Enviar(HttpMethod.Get, "/api/publicaciones/cupo" + (asesorId is null ? "" : $"?asesorId={asesorId}"),
            token), HttpStatusCode.OK);

    public static Task<Publicacion> PubDb(this InventarioApi api, long id) =>
        api.Leer(db => db.Publicaciones.AsNoTracking().SingleAsync(p => p.Id == id));

    public static Task<List<HistorialVigencia>> VigenciasDb(this InventarioApi api, long id) =>
        api.Leer(db => db.HistorialVigencias.AsNoTracking().Where(v => v.PublicacionId == id).OrderBy(v => v.Id).ToListAsync());

    public static Task<List<PublicacionDestacada>> DestacadosDb(this InventarioApi api, long id) =>
        api.Leer(db => db.PublicacionesDestacadas.AsNoTracking().Where(d => d.PublicacionId == id).OrderBy(d => d.Id).ToListAsync());

    // ---------------------------------------------------------------- venta y reasignación

    public static Task<HttpResponseMessage> Vender(this InventarioApi api, string? token, long vehiculoId, object? cuerpo = null) =>
        api.Enviar(HttpMethod.Post, $"/api/inventario/vehiculos/{vehiculoId}/vender", token,
            cuerpo ?? new { fechaVenta = HoyT, precioFinalCop = 95_000_000, origen = "PORTAL" });

    public static Task<HttpResponseMessage> RevertirVenta(this InventarioApi api, string? token, long vehiculoId,
        string? motivo = "Se cayó el negocio (prueba)") =>
        api.Enviar(HttpMethod.Post, $"/api/inventario/vehiculos/{vehiculoId}/revertir-venta", token, new { motivo });

    public static Task<HttpResponseMessage> Reasignar(this InventarioApi api, string? token, long vehiculoId, long? asesorId,
        string? motivo = "Cambio de zona del asesor (prueba)") =>
        api.Enviar(HttpMethod.Post, $"/api/admin/vehiculos/{vehiculoId}/reasignar", token, new { asesorId, motivo });

    public static Task<List<VentaVehiculo>> VentasDb(this InventarioApi api, long vehiculoId) =>
        api.Leer(db => db.VentasVehiculo.AsNoTracking().Where(v => v.VehiculoId == vehiculoId).OrderBy(v => v.Id).ToListAsync());

    // ---------------------------------------------------------------- portal público

    public static async Task<JsonElement[]> TarjetasPortal(this InventarioApi api)
    {
        var json = await Esperar(await api.Enviar(HttpMethod.Get, "/api/catalogo/publicaciones?tamano=48", null), HttpStatusCode.OK);
        return json.GetProperty("items").EnumerateArray().ToArray();
    }

    public static async Task<string[]> SlugsPortal(this InventarioApi api) =>
        (await api.TarjetasPortal()).Select(x => x.GetProperty("slug").GetString()!).ToArray();

    /// <summary>Slugs marcados como destacados en la búsqueda pública (sin el tope de 8 de /inicio).</summary>
    public static async Task<string[]> DestacadosPortal(this InventarioApi api) =>
        (await api.TarjetasPortal()).Where(x => x.GetProperty("destacado").GetBoolean())
            .Select(x => x.GetProperty("slug").GetString()!).ToArray();

    public static Task<HttpResponseMessage> DetallePortal(this InventarioApi api, string slug) =>
        api.Enviar(HttpMethod.Get, "/api/catalogo/publicaciones/" + Uri.EscapeDataString(slug), null);

    public static long IdDe(JsonElement e) => e.GetProperty("id").GetInt64();
    public static string Slug(JsonElement e) => e.GetProperty("slugPublico").GetString()!;
    public static string Texto(JsonElement e, string campo) => e.GetProperty(campo).GetString()!;
}

/// <summary>Fixture por clase para P4b: API de inventario sembrada con un reloj fijo en T.</summary>
public sealed class P4bFixture : IAsyncLifetime
{
    public InventarioApi Api { get; private set; } = null!;
    public async Task InitializeAsync() => Api = await InventarioApi.Crear(AyudasP4p.NuevoReloj());
    public Task DisposeAsync()
    {
        Api.Dispose();
        return Task.CompletedTask;
    }
}
