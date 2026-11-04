using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Camion.Api.Tests.Infraestructura;
using static Camion.Api.Tests.Infraestructura.Ayudas;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;

namespace Camion.Api.Tests;

/// <summary>
/// D-42 — foto de portada del portal: el administrador la sube (jpg, png o webp de hasta 5 MB) o la quita;
/// el portal la consulta sin token. Sin base de datos nueva: el archivo vive en Media:Ruta/portal y se audita.
/// Cada prueba usa su propia API (carpeta de medios aislada) porque la portada es única.
/// </summary>
public sealed class PortadaTests
{
    private const string Publica = "/api/catalogo/portada";
    private const string Admin = "/api/admin/portal/portada";
    private const string Quitar = "/api/admin/portal/portada/quitar";

    private static async Task<HttpResponseMessage> Subir(InventarioApi api, string? token, byte[] contenido,
        string nombre = "portada.jpg", string campo = "archivo")
    {
        var form = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(archivo, campo, nombre);
        var req = ConToken(HttpMethod.Post, Admin, token);
        req.Content = form;
        return await api.C.SendAsync(req);
    }

    private static async Task<string?> UrlPublica(InventarioApi api)
    {
        var r = await api.C.GetAsync(Publica);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var url = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("url");
        return url.ValueKind == JsonValueKind.Null ? null : url.GetString();
    }

    private static string[] ArchivosPortal(InventarioApi api)
    {
        var carpeta = Path.Combine(api.F.CarpetaMedia, "portal");
        return Directory.Exists(carpeta) ? Directory.GetFiles(carpeta) : [];
    }

    [Fact(DisplayName = "D-42: sin portada subida, la consulta pública (sin token) responde url null")]
    public async Task Sin_portada_url_null()
    {
        using var api = await InventarioApi.Crear();
        Assert.Null(await UrlPublica(api));
    }

    [Fact(DisplayName = "D-42: subir y quitar la portada es solo del administrador (401 sin token; 403 asesor y comprador)")]
    public async Task Permisos()
    {
        using var api = await InventarioApi.Crear();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Subir(api, null, InventarioApi.Jpeg())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Subir(api, api.A.Token, InventarioApi.Jpeg())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Subir(api, api.TokenComprador, InventarioApi.Jpeg())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Enviar(HttpMethod.Post, Quitar, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Enviar(HttpMethod.Post, Quitar, api.A.Token)).StatusCode);
        Assert.Empty(ArchivosPortal(api));
    }

    [Fact(DisplayName = "D-42: el administrador sube una portada; el portal la ve y /media la sirve; queda auditada")]
    public async Task Subir_portada()
    {
        using var api = await InventarioApi.Crear();
        var jpeg = InventarioApi.Jpeg(2048);
        var r = await Subir(api, api.Admin.Token, jpeg, "mi foto ../../x.jpg");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var url = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("url").GetString()!;
        Assert.StartsWith("/media/portal/portada-", url);
        Assert.DoesNotContain("mi foto", url);
        Assert.Equal(url, await UrlPublica(api));
        Assert.Equal(jpeg, await (await api.C.GetAsync(url)).Content.ReadAsByteArrayAsync());

        var evento = Assert.Single(await api.Auditoria("Portal", "portada"));
        Assert.Equal("CAMBIAR_PORTADA", evento.Accion);
        Assert.Equal(api.Admin.Id, evento.UsuarioId);
        Assert.Null(evento.DatosAnteriores);
    }

    [Fact(DisplayName = "D-42: una portada nueva reemplaza a la anterior (solo queda un archivo) y la auditoría guarda ambas")]
    public async Task Reemplazar_portada()
    {
        using var api = await InventarioApi.Crear();
        Assert.Equal(HttpStatusCode.OK, (await Subir(api, api.Admin.Token, InventarioApi.Jpeg())).StatusCode);
        var primera = await UrlPublica(api);
        Assert.Equal(HttpStatusCode.OK, (await Subir(api, api.Admin.Token, InventarioApi.Png(), "otra.png")).StatusCode);
        var segunda = await UrlPublica(api);
        Assert.NotEqual(primera, segunda);
        Assert.EndsWith(".png", segunda);
        Assert.Single(ArchivosPortal(api));
        Assert.Equal(HttpStatusCode.NotFound, (await api.C.GetAsync(primera)).StatusCode);
        var eventos = await api.Auditoria("Portal", "portada");
        Assert.Equal(2, eventos.Count);
        Assert.Contains(eventos, e => e.DatosAnteriores is not null && e.DatosAnteriores.Contains("portal/portada-"));
    }

    [Fact(DisplayName = "D-42: quitar la portada vuelve a la ilustración (url null), borra el archivo y se audita; repetirlo no falla")]
    public async Task Quitar_portada()
    {
        using var api = await InventarioApi.Crear();
        Assert.Equal(HttpStatusCode.OK, (await Subir(api, api.Admin.Token, InventarioApi.Webp())).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await api.Enviar(HttpMethod.Post, Quitar, api.Admin.Token)).StatusCode);
        Assert.Null(await UrlPublica(api));
        Assert.Empty(ArchivosPortal(api));
        Assert.Equal(HttpStatusCode.NoContent, (await api.Enviar(HttpMethod.Post, Quitar, api.Admin.Token)).StatusCode);
        var eventos = await api.Auditoria("Portal", "portada");
        Assert.Equal(new[] { "CAMBIAR_PORTADA", "QUITAR_PORTADA" }, eventos.Select(e => e.Accion).OrderBy(a => a).ToArray());
    }

    public static TheoryData<string, byte[]> NoValidas => new()
    {
        { "texto con extensión jpg", "hola"u8.ToArray() },
        { "gif", "GIF89a0000000"u8.ToArray() },
        { "vacía", [] },
        { "más de 5 MB", InventarioApi.Jpeg(5 * 1024 * 1024 + 1) },
    };

    [Theory(DisplayName = "D-42: solo imágenes JPG, PNG o WebP de hasta 5 MB, reconocidas por su contenido (400)")]
    [MemberData(nameof(NoValidas))]
    public async Task Rechaza_no_validas(string caso, byte[] contenido)
    {
        _ = caso;
        using var api = await InventarioApi.Crear();
        await Error(await Subir(api, api.Admin.Token, contenido), HttpStatusCode.BadRequest,
            "Sube una imagen JPG, PNG o WebP de hasta 5 MB.");
        Assert.Empty(ArchivosPortal(api));
        Assert.Null(await UrlPublica(api));
    }

    [Fact(DisplayName = "D-42: sin el campo \"archivo\" responde 400 y no guarda nada")]
    public async Task Sin_campo_archivo()
    {
        using var api = await InventarioApi.Crear();
        await Error(await Subir(api, api.Admin.Token, InventarioApi.Jpeg(), campo: "otro"), HttpStatusCode.BadRequest,
            "Sube una imagen JPG, PNG o WebP de hasta 5 MB.");
        Assert.Empty(ArchivosPortal(api));
    }
}
