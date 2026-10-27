using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Camion.Api.Data;
using Camion.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Camion.Api.Tests.Infraestructura.Ayudas;
using static Camion.Api.Tests.Infraestructura.AyudasP2;

namespace Camion.Api.Tests.Infraestructura;

/// <summary>Asesor de prueba creado por la API (registro + aprobación del administrador). PerfilId = perfiles_asesor.id.</summary>
public sealed record AsesorPrueba(string UsuarioId, string Email, long PerfilId, string Token, string Nombre);

/// <summary>
/// P4a: API del inventario en memoria (SQLite de <see cref="CamionApiFactory"/>, fotos en una carpeta temporal).
/// Siembra catálogos ficticios (con entradas inactivas), un administrador, dos asesores ACTIVA (A y B), un asesor
/// PENDIENTE y un comprador. Ningún dato es real.
/// </summary>
public sealed class InventarioApi : IDisposable
{
    public const string MsgRevisar = "Revisa los datos marcados.";
    public const string MsgFiltros = "Revisa los filtros.";
    public const string MsgNoEncontrado = "Vehículo no encontrado.";
    public const string MsgFotoNoEncontrada = "Foto no encontrada.";
    public const string MsgPlacaDuplicada = "Esta placa ya está registrada en otro vehículo.";
    public const string MsgPlaca = "Escribe una placa válida (letras y números).";
    public const string MsgFoto = "Sube una imagen JPG, PNG o WebP de hasta 5 MB.";
    public const string MsgMaxFotos = "Ya hay 10 fotos; elimina alguna para subir otra.";
    public const string MsgFotosVendido = "Las fotos de un vehículo vendido no se pueden modificar.";
    public const string MsgEditarVendido = "No se puede editar un vehículo en estado vendido.";
    public const string MsgOrdenFotos = "Incluye todas las fotos del vehículo, cada una una sola vez.";
    public const string MsgTipoCambio = "La categoría de un vehículo no se puede cambiar.";
    public const string MsgAsesor = "Elige un asesor activo.";
    public const string MsgArchivoNoEncontrado = "Archivo no encontrado.";

    public static readonly int Anio = DateTime.UtcNow.Year;

    public CamionApiFactory F { get; } = new();
    public HttpClient C { get; }

    public long Carro, Camion, Carroceria, Maquinaria, Remolque;
    public long Chevrolet, Kenworth, Dina, Npr, T800, DinaLinner;
    public long Furgon, Estacas, CarroceriaInactiva, Aluminio, MaterialInactivo, Retro, MaquinaInactiva, Diesel, CombustibleInactivo;
    public long Medellin, Bogota;
    public (string Id, string Email, string Token) Admin;
    public AsesorPrueba A = null!, B = null!;
    public (string Id, long PerfilId) Pendiente;
    public string TokenComprador = null!;

    private InventarioApi() => C = F.CreateClient();

    public static async Task<InventarioApi> Crear()
    {
        var api = new InventarioApi();
        await api.SembrarCatalogos();
        api.Admin = await CrearAdministrador(api.F, api.C);
        api.A = await api.CrearAsesor("Asesora Alfa Ficticia");
        api.B = await api.CrearAsesor("Asesor Beta Ficticio");
        var pendiente = await CrearAsesorPendiente(api.C, "Asesor Pendiente Ficticio");
        api.Pendiente = (pendiente.Id, (await Perfil(api.F, pendiente.Id))!.Id);
        api.TokenComprador = await RegistrarYObtenerToken(api.C);
        return api;
    }

    private async Task SembrarCatalogos()
    {
        using var scope = F.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CamionDbContext>();
        var carro = new TipoVehiculo { Nombre = "Carro" };
        var camion = new TipoVehiculo { Nombre = "Camión" };
        var carroceria = new TipoVehiculo { Nombre = "Carrocería" };
        var maquinaria = new TipoVehiculo { Nombre = "Maquinaria" };
        var remolque = new TipoVehiculo { Nombre = "Remolque", Activo = false };
        db.TiposVehiculo.AddRange(carro, camion, carroceria, maquinaria, remolque);
        var chevrolet = new Marca { Nombre = "Chevrolet" };
        var kenworth = new Marca { Nombre = "Kenworth" };
        var dina = new Marca { Nombre = "Dina", Activo = false };
        db.Marcas.AddRange(chevrolet, kenworth, dina);
        var npr = new Linea { Marca = chevrolet, Nombre = "NPR" };
        var t800 = new Linea { Marca = kenworth, Nombre = "T800" };
        var linner = new Linea { Marca = dina, Nombre = "Linner" };
        db.Lineas.AddRange(npr, t800, linner);
        var furgon = new TipoCarroceria { Nombre = "Furgón" };
        var estacas = new TipoCarroceria { Nombre = "Estacas" };
        var carrInactiva = new TipoCarroceria { Nombre = "Niñera", Activo = false };
        db.TiposCarroceria.AddRange(furgon, estacas, carrInactiva);
        var aluminio = new MaterialCarroceria { Nombre = "Aluminio" };
        var matInactivo = new MaterialCarroceria { Nombre = "Cartón", Activo = false };
        db.MaterialesCarroceria.AddRange(aluminio, matInactivo);
        var retro = new TipoMaquina { Nombre = "Retroexcavadora" };
        var maqInactiva = new TipoMaquina { Nombre = "Grúa antigua", Activo = false };
        db.TiposMaquina.AddRange(retro, maqInactiva);
        var diesel = new Combustible { Nombre = "Diésel" };
        var combInactivo = new Combustible { Nombre = "Carbón", Activo = false };
        db.Combustibles.AddRange(diesel, combInactivo);
        var medellin = new Municipio { Departamento = new Departamento { Codigo = "05", Nombre = "Antioquia" }, Codigo = "05001", Nombre = "Medellín" };
        var bogota = new Municipio { Departamento = new Departamento { Codigo = "11", Nombre = "Bogotá D.C." }, Codigo = "11001", Nombre = "Bogotá" };
        db.Municipios.AddRange(medellin, bogota);
        await db.SaveChangesAsync();

        (Carro, Camion, Carroceria, Maquinaria, Remolque) = (carro.Id, camion.Id, carroceria.Id, maquinaria.Id, remolque.Id);
        (Chevrolet, Kenworth, Dina, Npr, T800, DinaLinner) = (chevrolet.Id, kenworth.Id, dina.Id, npr.Id, t800.Id, linner.Id);
        (Furgon, Estacas, CarroceriaInactiva) = (furgon.Id, estacas.Id, carrInactiva.Id);
        (Aluminio, MaterialInactivo, Retro, MaquinaInactiva) = (aluminio.Id, matInactivo.Id, retro.Id, maqInactiva.Id);
        (Diesel, CombustibleInactivo, Medellin, Bogota) = (diesel.Id, combInactivo.Id, medellin.Id, bogota.Id);
    }

    /// <summary>Registra un asesor (queda PENDIENTE), el administrador lo aprueba y se obtiene su token.</summary>
    public async Task<AsesorPrueba> CrearAsesor(string nombre)
    {
        var (id, email, _) = await CrearAsesorPendiente(C, nombre);
        await Ok(await AccionMotivo(C, Admin.Token, id, "aprobar", null));
        var perfil = (await Perfil(F, id))!;
        return new AsesorPrueba(id, email, perfil.Id, await ObtenerToken(C, email), nombre);
    }

    // ---------------------------------------------------------------- cuerpos válidos por categoría (contrato P4a)

    public static string PlacaUnica()
    {
        const string letras = "ABCDEFGHJKLMNPRSTUVWXYZ";
        var r = Random.Shared;
        return $"{letras[r.Next(letras.Length)]}{letras[r.Next(letras.Length)]}{letras[r.Next(letras.Length)]}{r.Next(100, 1000)}";
    }

    public Dictionary<string, object?> CuerpoCarro(long? asesorId = null) => new()
    {
        ["tipoId"] = Carro, ["marcaId"] = Chevrolet, ["modeloAnio"] = 2020, ["kilometraje"] = 45_000,
        ["municipioId"] = Medellin, ["precioCop"] = 50_000_000, ["asesorId"] = asesorId
    };

    public Dictionary<string, object?> CuerpoCamion(long? asesorId = null) => new()
    {
        ["tipoId"] = Camion, ["marcaId"] = Chevrolet, ["lineaId"] = Npr, ["modeloAnio"] = 2019, ["kilometraje"] = 120_000,
        ["municipioId"] = Medellin, ["precioCop"] = 100_000_000, ["asesorId"] = asesorId
    };

    public Dictionary<string, object?> CuerpoCarroceria(long? asesorId = null) => new()
    {
        ["tipoId"] = Carroceria, ["carroceriaId"] = Furgon, ["municipioId"] = Medellin, ["precioCop"] = 20_000_000,
        ["asesorId"] = asesorId
    };

    public Dictionary<string, object?> CuerpoMaquinaria(long? asesorId = null) => new()
    {
        ["tipoId"] = Maquinaria, ["marcaId"] = Kenworth, ["modeloAnio"] = 2018, ["tipoMaquinaId"] = Retro,
        ["municipioId"] = Bogota, ["precioCop"] = 300_000_000, ["asesorId"] = asesorId
    };

    public Dictionary<string, object?> Cuerpo(string categoria, long? asesorId = null) => categoria switch
    {
        "carro" => CuerpoCarro(asesorId),
        "camion" => CuerpoCamion(asesorId),
        "carroceria" => CuerpoCarroceria(asesorId),
        "maquinaria" => CuerpoMaquinaria(asesorId),
        _ => throw new ArgumentException(categoria)
    };

    public static Dictionary<string, object?> Con(Dictionary<string, object?> cuerpo, params (string Campo, object? Valor)[] cambios)
    {
        var copia = new Dictionary<string, object?>(cuerpo);
        foreach (var (campo, valor) in cambios) copia[campo] = valor;
        return copia;
    }

    public static Dictionary<string, object?> Sin(Dictionary<string, object?> cuerpo, params string[] campos)
    {
        var copia = new Dictionary<string, object?>(cuerpo);
        foreach (var campo in campos) copia.Remove(campo);
        return copia;
    }

    // ---------------------------------------------------------------- llamadas

    public Task<HttpResponseMessage> Enviar(HttpMethod metodo, string url, string? token, object? cuerpo = null)
    {
        var req = ConToken(metodo, url, token);
        if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo);
        return C.SendAsync(req);
    }

    public Task<HttpResponseMessage> Crear(string? token, object cuerpo) =>
        Enviar(HttpMethod.Post, "/api/inventario/vehiculos", token, cuerpo);

    public Task<HttpResponseMessage> Editar(string? token, long id, object cuerpo) =>
        Enviar(HttpMethod.Put, $"/api/inventario/vehiculos/{id}", token, cuerpo);

    public Task<HttpResponseMessage> Detalle(string? token, long id) =>
        Enviar(HttpMethod.Get, $"/api/inventario/vehiculos/{id}", token);

    public Task<HttpResponseMessage> Lista(string? token, string query = "") =>
        Enviar(HttpMethod.Get, "/api/inventario/vehiculos" + query, token);

    public Task<HttpResponseMessage> Accion(string? token, long id, string accion, string? motivo = null) =>
        Enviar(HttpMethod.Post, $"/api/inventario/vehiculos/{id}/{accion}", token, new { motivo });

    /// <summary>Crea un vehículo y devuelve su detalle (201).</summary>
    public async Task<JsonElement> Creado(string token, object cuerpo)
    {
        var r = await Crear(token, cuerpo);
        var texto = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == HttpStatusCode.Created, $"Se esperaba 201 y llegó {(int)r.StatusCode}: {texto}");
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    public async Task<long> CrearId(AsesorPrueba asesor, string categoria = "camion", params (string, object?)[] cambios) =>
        (await Creado(asesor.Token, Con(Cuerpo(categoria), cambios))).GetProperty("id").GetInt64();

    // ---------------------------------------------------------------- fotos

    public static byte[] Jpeg(int tamano = 64) => Imagen(tamano, 0xFF, 0xD8, 0xFF, 0xE0);
    public static byte[] Png(int tamano = 64) => Imagen(tamano, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
    public static byte[] Webp(int tamano = 64) =>
        Imagen(tamano, (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0x10, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P');

    private static byte[] Imagen(int tamano, params byte[] firma)
    {
        var bytes = new byte[Math.Max(tamano, firma.Length)];
        Random.Shared.NextBytes(bytes);
        firma.CopyTo(bytes, 0);
        return bytes;
    }

    public Task<HttpResponseMessage> SubirFoto(string? token, long id, byte[] contenido, string nombre = "foto.jpg",
        string tipo = "image/jpeg", string campo = "archivo")
    {
        var form = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        form.Add(archivo, campo, nombre);
        var req = ConToken(HttpMethod.Post, $"/api/inventario/vehiculos/{id}/fotos", token);
        req.Content = form;
        return C.SendAsync(req);
    }

    public async Task<JsonElement> FotoSubida(string token, long id, byte[]? contenido = null)
    {
        var r = await SubirFoto(token, id, contenido ?? Jpeg());
        var texto = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == HttpStatusCode.Created, $"Se esperaba 201 y llegó {(int)r.StatusCode}: {texto}");
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    public Task<HttpResponseMessage> BorrarFoto(string? token, long id, long fotoId) =>
        Enviar(HttpMethod.Delete, $"/api/inventario/vehiculos/{id}/fotos/{fotoId}", token);

    public Task<HttpResponseMessage> Ordenar(string? token, long id, object cuerpo) =>
        Enviar(HttpMethod.Put, $"/api/inventario/vehiculos/{id}/fotos/orden", token, cuerpo);

    /// <summary>Ruta física de una URL /media/... dentro de la carpeta temporal.</summary>
    public string RutaFisica(string url) =>
        Path.Combine(F.CarpetaMedia, Uri.UnescapeDataString(url["/media/".Length..]).Replace('/', Path.DirectorySeparatorChar));

    public int ArchivosDe(long vehiculoId)
    {
        var carpeta = Path.Combine(F.CarpetaMedia, "vehiculos", vehiculoId.ToString());
        return Directory.Exists(carpeta) ? Directory.GetFiles(carpeta).Length : 0;
    }

    // ---------------------------------------------------------------- base

    public async Task Db(Func<CamionDbContext, Task> accion)
    {
        using var scope = F.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CamionDbContext>();
        await accion(db);
        await db.SaveChangesAsync();
    }

    public Task<T> Leer<T>(Func<CamionDbContext, Task<T>> consulta) => AyudasP2.Db(F, consulta);

    public Task MarcarVendido(long id) => Db(async db => (await db.Vehiculos.SingleAsync(v => v.Id == id)).Estado = global::Camion.Api.Models.Vehiculo.Vendido);

    public Task<List<AuditoriaEvento>> AuditoriaVehiculo(long id) =>
        Leer(db => db.AuditoriaEventos.AsNoTracking()
            .Where(a => a.Entidad == "Vehiculo" && a.EntidadId == id.ToString()).OrderBy(a => a.Id).ToListAsync());

    public Task<Vehiculo> VehiculoDb(long id) => Leer(db => db.Vehiculos.AsNoTracking().SingleAsync(v => v.Id == id));

    public void Dispose()
    {
        C.Dispose();
        F.Dispose();
    }
}

/// <summary>Fixture compartida por clase: una API de inventario sembrada. Las pruebas crean sus propios vehículos.</summary>
public sealed class InventarioFixture : IAsyncLifetime
{
    public InventarioApi Api { get; private set; } = null!;
    public async Task InitializeAsync() => Api = await InventarioApi.Crear();
    public Task DisposeAsync()
    {
        Api.Dispose();
        return Task.CompletedTask;
    }
}

public static class InventarioJson
{
    public static async Task<string[]> Errores(HttpResponseMessage r, HttpStatusCode codigo, string campo, string? error = null)
    {
        var texto = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == codigo, $"Se esperaba {(int)codigo} y llegó {(int)r.StatusCode}: {texto}");
        var json = JsonDocument.Parse(texto).RootElement;
        if (error is not null) Assert.Equal(error, json.GetProperty("error").GetString());
        var lista = default(JsonElement);
        Assert.True(json.TryGetProperty("errores", out var errores) && errores.TryGetProperty(campo, out lista),
            $"Falta errores.{campo} en {texto}");
        return lista.EnumerateArray().Select(x => x.GetString()!).ToArray();
    }

    /// <summary>400 { error: "Revisa los datos marcados.", errores: { campo: [mensaje] } }.</summary>
    public static async Task Afirmar400(HttpResponseMessage r, string campo, string mensaje) =>
        Assert.Equal(new[] { mensaje }, await Errores(r, HttpStatusCode.BadRequest, campo, InventarioApi.MsgRevisar));

    public static async Task<JsonElement> Json(HttpResponseMessage r, HttpStatusCode codigo)
    {
        var texto = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == codigo, $"Se esperaba {(int)codigo} y llegó {(int)r.StatusCode}: {texto}");
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    public static bool EsNulo(JsonElement e, string campo) =>
        !e.TryGetProperty(campo, out var v) || v.ValueKind == JsonValueKind.Null;
}
