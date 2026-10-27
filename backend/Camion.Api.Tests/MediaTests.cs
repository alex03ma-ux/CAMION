using System.Net;
using Camion.Api.Media;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace Camion.Api.Tests;

/// <summary>
/// P4a (D-20) — GET /media/{**ruta}: anónimo, solo jpg/jpeg/png/webp de la carpeta Media:Ruta (aquí una carpeta
/// temporal), con Cache-Control y nosniff; rechaza "..", rutas absolutas, unidades y archivos fuera de la raíz (404).
/// </summary>
public sealed class MediaTests : IClassFixture<CamionApiFactory>, IDisposable
{
    private readonly CamionApiFactory _f;
    private readonly HttpClient _c;
    // Estático: la fixture (carpeta) es compartida y los archivos se escriben una sola vez.
    private static readonly byte[] _jpeg = InventarioApi.Jpeg(512);

    public MediaTests(CamionApiFactory f)
    {
        _f = f;
        _c = f.CreateClient();
        Escribir(_f.CarpetaMedia, "vehiculos/1/foto.jpg", _jpeg);
        Escribir(_f.CarpetaMedia, "vehiculos/1/foto.jpeg", _jpeg);
        Escribir(_f.CarpetaMedia, "vehiculos/1/foto.png", InventarioApi.Png(256));
        Escribir(_f.CarpetaMedia, "vehiculos/1/foto.webp", InventarioApi.Webp(256));
        Escribir(_f.CarpetaMedia, "vehiculos/1/MAYUS.JPG", _jpeg);
        Escribir(_f.CarpetaMedia, "pub 1/foto ñ.jpg", _jpeg);
        Escribir(_f.CarpetaMedia, "vehiculos/1/nota.txt", "texto"u8.ToArray());
        Escribir(_f.CarpetaMedia, "vehiculos/1/animada.gif", "GIF89a"u8.ToArray());
        Escribir(_f.CarpetaMedia, "vehiculos/1/sin-extension", _jpeg);
        Escribir(_f.CarpetaMedia, "appsettings.json", "{}"u8.ToArray());
        Directory.CreateDirectory(Path.Combine(_f.CarpetaMedia, "vehiculos", "carpeta.jpg"));
        // Fuera de la raíz: una carpeta hermana cuyo nombre empieza igual que la raíz (p. ej. C:\CAMION_media_fuera).
        Escribir(_f.CarpetaFueraDeMedia, "secreto.jpg", _jpeg);
    }

    private static void Escribir(string raiz, string relativa, byte[] contenido)
    {
        var ruta = Path.Combine(raiz, relativa.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        if (!File.Exists(ruta)) File.WriteAllBytes(ruta, contenido);
    }

    /// <summary>Petición con la ruta tal cual (sin la normalización de "." y ".." que hace System.Uri en el cliente).</summary>
    private async Task<int> GetCrudo(string ruta)
    {
        var ctx = await _f.Server.SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Get;
            c.Request.Path = new PathString(ruta);
        });
        return ctx.Response.StatusCode;
    }

    [Theory(DisplayName = "D-20: sirve jpg, jpeg, png y webp sin token con su Content-Type, Cache-Control public, max-age=86400 y nosniff")]
    [InlineData("/media/vehiculos/1/foto.jpg", "image/jpeg")]
    [InlineData("/media/vehiculos/1/foto.jpeg", "image/jpeg")]
    [InlineData("/media/vehiculos/1/foto.png", "image/png")]
    [InlineData("/media/vehiculos/1/foto.webp", "image/webp")]
    [InlineData("/media/vehiculos/1/MAYUS.JPG", "image/jpeg")]
    [InlineData("/media/pub%201/foto%20%C3%B1.jpg", "image/jpeg")]
    public async Task Sirve_imagen(string url, string tipo)
    {
        var r = await _c.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(tipo, r.Content.Headers.ContentType!.MediaType);
        Assert.Equal("public, max-age=86400", r.Headers.CacheControl!.ToString());
        Assert.Equal("nosniff", Assert.Single(r.Headers.GetValues("X-Content-Type-Options")));
        var bytes = await r.Content.ReadAsByteArrayAsync();
        if (tipo == "image/jpeg") Assert.Equal(_jpeg, bytes);
    }

    [Fact(DisplayName = "D-20: la URL que genera el catálogo (UrlFoto, segmentos codificados) se sirve")]
    public async Task Url_del_catalogo_se_sirve()
    {
        var url = Camion.Api.Catalogo.ConsultasCatalogo.UrlFoto("pub 1/foto ñ.jpg")!;
        Assert.Equal(HttpStatusCode.OK, (await _c.GetAsync(url)).StatusCode);
    }

    [Theory(DisplayName = "D-20: extensiones no imagen, sin extensión, carpetas o archivos inexistentes -> 404 { error }")]
    [InlineData("/media/vehiculos/1/nota.txt")]
    [InlineData("/media/vehiculos/1/animada.gif")]
    [InlineData("/media/vehiculos/1/sin-extension")]
    [InlineData("/media/appsettings.json")]
    [InlineData("/media/vehiculos/carpeta.jpg")]
    [InlineData("/media/vehiculos/1/no-existe.jpg")]
    [InlineData("/media/")]
    [InlineData("/media")]
    public async Task No_imagen_o_inexistente_404(string url)
    {
        var r = await _c.GetAsync(url);
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        var texto = await r.Content.ReadAsStringAsync();
        if (texto.Length > 0) Assert.Contains(InventarioApi.MsgArchivoNoEncontrado, texto);
    }

    [Theory(DisplayName = "D-20: \"..\", \".\", barras invertidas, rutas absolutas, unidades y UNC -> 404 (nunca se sale de la raíz)")]
    [InlineData("/media/../RAIZ_fuera/secreto.jpg")]
    [InlineData("/media/vehiculos/../../RAIZ_fuera/secreto.jpg")]
    [InlineData("/media/..\\RAIZ_fuera\\secreto.jpg")]
    [InlineData("/media/vehiculos/./1/foto.jpg")]
    [InlineData("/media/vehiculos/1/../1/foto.jpg")]
    [InlineData("/media//ABS")]
    [InlineData("/media/ABS")]
    [InlineData("/media/ABS_BARRAS")]
    [InlineData("/media/\\\\localhost\\c$\\secreto.jpg")]
    [InlineData("/media/vehiculos/1/foto.jpg:flujo.jpg")]
    [InlineData("/media/vehiculos/1/foto.jpg%00.jpg")]
    public async Task Rutas_peligrosas_404(string ruta)
    {
        var raiz = Path.GetFileName(Path.TrimEndingDirectorySeparator(_f.CarpetaMedia));
        var absoluta = Path.Combine(_f.CarpetaFueraDeMedia, "secreto.jpg");
        ruta = ruta.Replace("RAIZ", raiz)
            .Replace("ABS_BARRAS", absoluta)
            .Replace("ABS", absoluta.Replace('\\', '/'));
        Assert.True(File.Exists(absoluta)); // el archivo de fuera existe de verdad
        Assert.Equal(StatusCodes.Status200OK, await GetCrudo("/media/vehiculos/1/foto.jpg")); // control positivo
        Assert.Equal(StatusCodes.Status404NotFound, await GetCrudo(ruta));
    }

    [Theory(DisplayName = "D-20: \"..\" codificado en la URL (%2E%2E, %2F, %5C) -> 404")]
    [InlineData("/media/%2E%2E/RAIZ_fuera/secreto.jpg")]
    [InlineData("/media/..%2FRAIZ_fuera%2Fsecreto.jpg")]
    [InlineData("/media/..%5CRAIZ_fuera%5Csecreto.jpg")]
    [InlineData("/media/%2e%2e%2f%2e%2e%2fWindows%2fwin.ini")]
    public async Task Rutas_codificadas_404(string url)
    {
        var raiz = Path.GetFileName(Path.TrimEndingDirectorySeparator(_f.CarpetaMedia));
        var r = await _c.GetAsync(url.Replace("RAIZ", raiz));
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact(DisplayName = "D-20: /media solo admite GET (POST, PUT y DELETE -> 405, nunca 2xx) y no borra archivos")]
    public async Task Solo_get()
    {
        foreach (var metodo in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
        {
            var r = await _c.SendAsync(new HttpRequestMessage(metodo, "/media/vehiculos/1/foto.jpg"));
            Assert.Equal(HttpStatusCode.MethodNotAllowed, r.StatusCode);
        }
        Assert.True(File.Exists(Path.Combine(_f.CarpetaMedia, "vehiculos", "1", "foto.jpg")));
    }

    // ---------------------------------------------------------------- AlmacenFotosLocal (unidad)

    [Theory(DisplayName = "D-20: AlmacenFotosLocal.RutaSegura acepta solo rutas relativas dentro de la raíz")]
    [InlineData("vehiculos/1/a.jpg", true)]
    [InlineData("vehiculos\\1\\a.jpg", true)]
    [InlineData("../x.jpg", false)]
    [InlineData("a/../../x.jpg", false)]
    [InlineData("./a.jpg", false)]
    [InlineData("/a.jpg", false)]
    [InlineData("\\a.jpg", false)]
    [InlineData("C:/Windows/a.jpg", false)]
    [InlineData("C:a.jpg", false)]
    [InlineData("a.jpg:flujo", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("a\0.jpg", false)]
    public void Ruta_segura(string clave, bool dentro)
    {
        var raiz = Path.Combine(Path.GetTempPath(), $"camion_almacen_{Guid.NewGuid():N}");
        var almacen = new AlmacenFotosLocal(raiz);
        var ruta = almacen.RutaSegura(clave);
        if (dentro)
        {
            Assert.NotNull(ruta);
            Assert.StartsWith(Path.GetFullPath(raiz) + Path.DirectorySeparatorChar, ruta);
        }
        else
            Assert.Null(ruta);
    }

    [Fact(DisplayName = "D-20: AlmacenFotosLocal no confunde la raíz con una carpeta hermana de nombre parecido y nunca sobrescribe")]
    public async Task Almacen_prefijo_y_sin_sobrescribir()
    {
        var raiz = Path.Combine(Path.GetTempPath(), $"camion_almacen_{Guid.NewGuid():N}");
        try
        {
            var almacen = new AlmacenFotosLocal(raiz);
            Assert.Equal(Path.GetFullPath(raiz) + Path.DirectorySeparatorChar, almacen.Raiz);
            Assert.Null(almacen.RutaSegura($"../{Path.GetFileName(raiz)}2/x.jpg"));

            await almacen.GuardarAsync("v/1/a.jpg", new MemoryStream([1, 2, 3]), CancellationToken.None);
            await Assert.ThrowsAsync<IOException>(() => almacen.GuardarAsync("v/1/a.jpg", new MemoryStream([9]), CancellationToken.None));
            Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(almacen.RutaSegura("v/1/a.jpg")!));
            await Assert.ThrowsAsync<ArgumentException>(() => almacen.GuardarAsync("../fuera.jpg", new MemoryStream([1]), CancellationToken.None));
            Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), "fuera.jpg")) &&
                         File.GetCreationTimeUtc(Path.Combine(Path.GetTempPath(), "fuera.jpg")) > DateTime.UtcNow.AddMinutes(-1));

            almacen.Eliminar("v/1/a.jpg");
            Assert.False(File.Exists(Path.Combine(raiz, "v", "1", "a.jpg")));
            almacen.Eliminar("v/1/no-existe.jpg"); // no lanza
            almacen.Eliminar("../x.jpg");          // no lanza ni sale de la raíz
        }
        finally
        {
            if (Directory.Exists(raiz)) Directory.Delete(raiz, recursive: true);
        }
    }

    public void Dispose() => _c.Dispose();
}
