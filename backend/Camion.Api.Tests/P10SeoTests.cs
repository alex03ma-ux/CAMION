using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Camion.Api.Endpoints;
using Camion.Api.Models;
using Camion.Api.Seguridad;
using Camion.Api.Seo;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using static Camion.Api.Tests.Infraestructura.Ayudas;

namespace Camion.Api.Tests;

/// <summary>Plantilla ficticia con la forma del index.html que publica Vite, con las marcas de CA-P10-75.</summary>
public static class PlantillaPrueba
{
    public const string Antes =
        "<!doctype html>\n<html lang=\"es\">\n  <head>\n    <meta charset=\"UTF-8\" />\n    ";
    public const string BloqueGenerico =
        "\n    <title>Venta de camiones usados y crédito | Financiamos tu Camión</title>\n" +
        "    <meta name=\"description\" content=\"Descripción genérica ficticia.\" />\n    ";
    public const string Despues =
        "\n    <script type=\"module\" crossorigin src=\"/assets/index-Ab12Cd.js\"></script>\n" +
        "    <link rel=\"stylesheet\" crossorigin href=\"/assets/index-Ef34Gh.css\">\n  </head>\n" +
        "  <body>\n    <div id=\"root\"></div>\n  </body>\n</html>\n";
    public const string Completa =
        Antes + PlantillaPortal.MarcaInicio + BloqueGenerico + PlantillaPortal.MarcaFin + Despues;

    public static string Crear(string contenido = Completa)
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"camion_index_pruebas_{Guid.NewGuid():N}.html");
        File.WriteAllText(ruta, contenido);
        return ruta;
    }

    public static void Borrar(string ruta)
    {
        try { File.Delete(ruta); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Datos extra de P10: XSS en el título, una carrocería, un slug con caracteres especiales y placas.</summary>
    public static void Sembrar(Siembra s)
    {
        s.V1.Vehiculo!.Placa = "XYZ987";
        var xss = s.Pub("xss-2020", sinLinea: true, titulo: TituloXss);
        xss.DescripcionComercial = "</script><script>alert(2)</script> & más";
        xss.Vehiculo!.Placa = "ABC123";
        s.Pub("carroceria-furgon-2019", tipo: s.Carroceria, marca: s.Andinas, sinLinea: true, km: 1000, anio: 2019,
            titulo: "Furgón térmico");
        s.Pub("camion-ñ&x-2020", tipo: s.Camion);
    }

    public const string TituloXss = "\"><img src=x onerror=alert(1)></script><script>alert(1)</script>";
}

/// <summary>Fixture con la indexación encendida y la plantilla configurada (solo lectura).</summary>
public sealed class P10Encendida : IAsyncLifetime
{
    public CatalogoApi Api { get; private set; } = null!;
    public string Ruta { get; } = PlantillaPrueba.Crear();

    public async Task InitializeAsync() => Api = await CatalogoApi.Crear(extra: PlantillaPrueba.Sembrar, configurar: b =>
    {
        b.UseSetting(ConfiguracionPortal.ClavePermitirIndexacion, "true");
        b.UseSetting(ConfiguracionPortal.ClaveRutaIndexHtml, Ruta);
    });

    public Task DisposeAsync()
    {
        Api.Dispose();
        PlantillaPrueba.Borrar(Ruta);
        return Task.CompletedTask;
    }
}

/// <summary>Fixture con la configuración por defecto (indexación apagada, D-89 A) y la plantilla configurada.</summary>
public sealed class P10Apagada : IAsyncLifetime
{
    public CatalogoApi Api { get; private set; } = null!;
    public string Ruta { get; } = PlantillaPrueba.Crear();

    public async Task InitializeAsync() => Api = await CatalogoApi.Crear(extra: PlantillaPrueba.Sembrar,
        configurar: b => b.UseSetting(ConfiguracionPortal.ClaveRutaIndexHtml, Ruta));

    public Task DisposeAsync()
    {
        Api.Dispose();
        PlantillaPrueba.Borrar(Ruta);
        return Task.CompletedTask;
    }
}

/// <summary>P10: /robots.txt y /sitemap.xml (D-89 A; CA-P10-51 a 60, 84 a 86).</summary>
public sealed class P10RobotsSitemapTests(P10Encendida encendida, P10Apagada apagada)
    : IClassFixture<P10Encendida>, IClassFixture<P10Apagada>
{
    private const string Url = CamionApiFactory.UrlPortalPruebas;
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    private static async Task<string> Texto(HttpResponseMessage r, HttpStatusCode esperado)
    {
        var texto = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == esperado, $"se esperaba {(int)esperado} y llegó {(int)r.StatusCode}: {texto}");
        return texto;
    }

    private static async Task<XDocument> Sitemap(CatalogoApi api)
    {
        var r = await api.Get("/sitemap.xml");
        var texto = await Texto(r, HttpStatusCode.OK);
        Assert.Equal("application/xml; charset=utf-8", r.Content.Headers.ContentType!.ToString());
        Assert.Equal("public, max-age=3600", r.Headers.CacheControl!.ToString());
        return XDocument.Parse(texto);
    }

    private static string[] Locs(XDocument d) => d.Root!.Elements(Ns + "url").Select(u => u.Element(Ns + "loc")!.Value).ToArray();

    [Fact(DisplayName = "CA-P10-52 (D-89 A): por defecto robots.txt dice Disallow: / y no lleva Sitemap")]
    public async Task Robots_apagado()
    {
        var r = await apagada.Api.Get("/robots.txt");
        Assert.Equal("User-agent: *\nDisallow: /\n", await Texto(r, HttpStatusCode.OK));
        Assert.Equal("text/plain; charset=utf-8", r.Content.Headers.ContentType!.ToString());
        Assert.Equal("public, max-age=3600", r.Headers.CacheControl!.ToString());
    }

    [Fact(DisplayName = "CA-P10-51: con la indexación encendida, Allow de /api/catalogo/ y /api/portal/, Disallow de /api/ y /health, y Sitemap")]
    public async Task Robots_encendido()
    {
        var r = await encendida.Api.Get("/robots.txt");
        Assert.Equal("User-agent: *\nAllow: /api/catalogo/\nAllow: /api/portal/\nDisallow: /api/\nDisallow: /health\nAllow: /\n\n" +
                     $"Sitemap: {Url}/sitemap.xml\n", await Texto(r, HttpStatusCode.OK));
        Assert.Equal("text/plain; charset=utf-8", r.Content.Headers.ContentType!.ToString());
    }

    [Fact(DisplayName = "CA-P10-53 (D-89 A): con la indexación apagada, sitemap.xml responde 404")]
    public async Task Sitemap_apagado() =>
        await Texto(await apagada.Api.Get("/sitemap.xml"), HttpStatusCode.NotFound);

    [Fact(DisplayName = "CA-P10-55 a 57: páginas públicas, categorías con vehículos visibles y una entrada por publicación visible, con lastmod")]
    public async Task Sitemap_contenido()
    {
        var d = await Sitemap(encendida.Api);
        var locs = Locs(d);
        var esperadasFijas = new[]
        {
            Url + "/", Url + "/vehiculos", Url + "/carros", Url + "/camiones", Url + "/carrocerias", Url + "/credito",
            Url + "/vende-tu-camion", Url + "/seguro-todo-riesgo", Url + "/busco-un-vehiculo"
        };
        Assert.Equal(esperadasFijas, locs.Take(esperadasFijas.Length).ToArray());
        // Maquinaria no tiene vehículos visibles en la siembra.
        Assert.DoesNotContain(Url + "/maquinaria", locs);

        var fichas = locs.Skip(esperadasFijas.Length).ToArray();
        foreach (var slug in new[] { "chevrolet-npr-2020", "kenworth-t800-2018", "renault-logan-2021", "chevrolet-camion-2015",
                     "xss-2020", "carroceria-furgon-2019" })
            Assert.Contains($"{Url}/vehiculo/{slug}", fichas);
        // Slug con caracteres especiales: codificado para URL (y el XML lo escapa al escribirlo).
        Assert.Contains($"{Url}/vehiculo/{Uri.EscapeDataString("camion-ñ&x-2020")}", fichas);
        Assert.Equal(7, fichas.Length);

        // CA-P10-56: nada que no sea visible ni rutas privadas, de crédito o legales.
        foreach (var caso in Siembra.CasosNoVisibles) Assert.DoesNotContain($"{Url}/vehiculo/{caso}", locs);
        Assert.DoesNotContain(locs, l => l.Contains("/credito/") || l.Contains("/cuenta") || l.Contains("/restablecer") ||
                                         l.Contains("/privacidad") || l.Contains("/terminos") || l.Contains("/api/"));

        // CA-P10-57: lastmod AAAA-MM-DD en las fichas; sin changefreq ni priority.
        foreach (var u in d.Root!.Elements(Ns + "url"))
        {
            Assert.Null(u.Element(Ns + "changefreq"));
            Assert.Null(u.Element(Ns + "priority"));
            var esFicha = u.Element(Ns + "loc")!.Value.Contains("/vehiculo/");
            if (esFicha) Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", u.Element(Ns + "lastmod")!.Value);
            else Assert.Null(u.Element(Ns + "lastmod"));
        }
    }

    [Fact(DisplayName = "CA-P10-57: lastmod es la mayor entre publicaciones.actualizado_en y vehiculos.actualizado_en")]
    public async Task Sitemap_lastmod()
    {
        using var api = await CatalogoApi.Crear(noVisibles: false,
            configurar: b => b.UseSetting(ConfiguracionPortal.ClavePermitirIndexacion, "true"));
        await api.Db(async db =>
        {
            var v1 = await db.Publicaciones.Include(p => p.Vehiculo).SingleAsync(p => p.SlugPublico == "chevrolet-npr-2020");
            v1.ActualizadoEn = new DateTime(2026, 9, 1, 23, 0, 0, DateTimeKind.Utc);
            v1.Vehiculo!.ActualizadoEn = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
            var v2 = await db.Publicaciones.Include(p => p.Vehiculo).SingleAsync(p => p.SlugPublico == "kenworth-t800-2018");
            v2.ActualizadoEn = new DateTime(2026, 10, 25, 1, 0, 0, DateTimeKind.Utc);
            v2.Vehiculo!.ActualizadoEn = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        });
        var d = await Sitemap(api);
        string Lastmod(string slug) => d.Root!.Elements(Ns + "url")
            .Single(u => u.Element(Ns + "loc")!.Value == $"{Url}/vehiculo/{slug}").Element(Ns + "lastmod")!.Value;
        Assert.Equal("2026-10-20", Lastmod("chevrolet-npr-2020"));
        Assert.Equal("2026-10-25", Lastmod("kenworth-t800-2018"));
    }

    [Fact(DisplayName = "CA-P10-60 (RN-VE-014): un vehículo vendido sale del sitemap en la siguiente respuesta")]
    public async Task Sitemap_vendido()
    {
        using var api = await CatalogoApi.Crear(noVisibles: false,
            configurar: b => b.UseSetting(ConfiguracionPortal.ClavePermitirIndexacion, "true"));
        Assert.Contains($"{Url}/vehiculo/renault-logan-2021", Locs(await Sitemap(api)));
        await api.Db(async db =>
        {
            var p = await db.Publicaciones.SingleAsync(x => x.SlugPublico == "renault-logan-2021");
            p.EstadoComercial = Publicacion.Vendido;
        });
        var locs = Locs(await Sitemap(api));
        Assert.DoesNotContain($"{Url}/vehiculo/renault-logan-2021", locs);
        // Carro ya no tiene vehículos visibles: su categoría también sale.
        Assert.DoesNotContain(Url + "/carros", locs);
    }

    [Fact(DisplayName = "CA-P10-58: Portal:UrlPublica manda sobre Recuperacion:UrlPortal y se quita la barra final")]
    public async Task Sitemap_url_publica()
    {
        using var api = await CatalogoApi.Crear(noVisibles: false, configurar: b =>
        {
            b.UseSetting(ConfiguracionPortal.ClavePermitirIndexacion, "true");
            b.UseSetting(ConfiguracionPortal.ClaveUrlPublica, "https://www.ejemplo.test/");
        });
        Assert.EndsWith("Sitemap: https://www.ejemplo.test/sitemap.xml\n", await (await api.Get("/robots.txt")).Content.ReadAsStringAsync());
        var locs = Locs(await Sitemap(api));
        Assert.All(locs, l => Assert.StartsWith("https://www.ejemplo.test/", l));
        Assert.Contains("https://www.ejemplo.test/vehiculo/chevrolet-npr-2020", locs);
    }

    [Fact(DisplayName = "CA-P10-84: el sitemap no lleva datos del asesor, placas ni ids internos")]
    public async Task Sitemap_sin_datos_personales()
    {
        var texto = await (await encendida.Api.Get("/sitemap.xml")).Content.ReadAsStringAsync();
        var s = encendida.Api.Datos;
        foreach (var dato in new[] { s.A1.Usuario.Email!, s.A1.Usuario.PhoneNumber!, s.A1.Usuario.NombreCompleto!,
                     s.A1.Perfil.NombreComercial!, "XYZ987", "ABC123", "wa.me" })
            Assert.DoesNotContain(dato, texto);
    }

    [Fact(DisplayName = "CA-P10-85: robots y sitemap responden igual sin token, con token válido y con token no válido (sin 401)")]
    public async Task Igual_con_cualquier_token()
    {
        var api = encendida.Api;
        var valido = api.Firmar(api.Datos.A1.Usuario.Id, api.Datos.A1.Usuario.SessionVersion, "ASESOR");
        foreach (var ruta in new[] { "/robots.txt", "/sitemap.xml" })
        {
            var sin = await (await api.Get(ruta)).Content.ReadAsStringAsync();
            foreach (var token in new[] { valido, "token-no-valido", api.Firmar("no-existe", 0, "ADMINISTRADOR") })
            {
                var r = await api.Get(ruta, token);
                Assert.Equal(HttpStatusCode.OK, r.StatusCode);
                Assert.Equal(sin, await r.Content.ReadAsStringAsync());
            }
        }
    }

    [Fact(DisplayName = "CA-P10-59, CA-P10-86: solo GET y HEAD; POST, PUT y DELETE responden 405")]
    public async Task Metodos()
    {
        var api = encendida.Api;
        foreach (var ruta in new[] { "/robots.txt", "/sitemap.xml", "/vehiculo/chevrolet-npr-2020" })
        {
            Assert.Equal(HttpStatusCode.OK, (await api.Cliente.SendAsync(new HttpRequestMessage(HttpMethod.Head, ruta))).StatusCode);
            foreach (var metodo in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
                Assert.Equal(HttpStatusCode.MethodNotAllowed,
                    (await api.Cliente.SendAsync(new HttpRequestMessage(metodo, ruta))).StatusCode);
        }
    }

    [Fact(DisplayName = "CA-P10-59: robots, sitemap y ficha no escriben en la base ni en la auditoría")]
    public async Task No_escriben()
    {
        var api = encendida.Api;
        async Task<(int, int, int)> Contar() => await api.Leer(async db => (await db.AuditoriaEventos.CountAsync(),
            await db.Leads.CountAsync(), await db.Publicaciones.CountAsync()));
        var antes = await Contar();
        foreach (var ruta in new[] { "/robots.txt", "/sitemap.xml", "/vehiculo/chevrolet-npr-2020", "/vehiculo/no-existe" })
            await api.Get(ruta);
        Assert.Equal(antes, await Contar());
    }
}

/// <summary>P10 (D-88 A): /vehiculo/{slug} con el index.html del frontend como plantilla (CA-P10-70 a 79, 84, 85).</summary>
public sealed class P10VistaPreviaTests(P10Encendida encendida, P10Apagada apagada)
    : IClassFixture<P10Encendida>, IClassFixture<P10Apagada>
{
    private const string Url = CamionApiFactory.UrlPortalPruebas;

    private static async Task<string> Html(HttpResponseMessage r, HttpStatusCode esperado = HttpStatusCode.OK)
    {
        var texto = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == esperado, $"se esperaba {(int)esperado} y llegó {(int)r.StatusCode}: {texto}");
        Assert.Equal("text/html; charset=utf-8", r.Content.Headers.ContentType!.ToString());
        return texto;
    }

    /// <summary>Valor (decodificado) del atributo content de la etiqueta con ese name o property; falla si hay 0 o más de 1.</summary>
    private static string Meta(string html, string nombre)
    {
        var m = Regex.Matches(html, $"<meta (?:name|property)=\"{Regex.Escape(nombre)}\" content=\"([^\"]*)\"");
        Assert.True(m.Count == 1, $"{nombre}: {m.Count} etiquetas");
        return WebUtility.HtmlDecode(m[0].Groups[1].Value);
    }

    private static string Titulo(string html)
    {
        var m = Regex.Matches(html, "<title>([^<]*)</title>");
        Assert.Single(m);
        return WebUtility.HtmlDecode(m[0].Groups[1].Value);
    }

    private static JsonElement JsonLd(string html, string marca)
    {
        var m = Regex.Match(html, $"<script type=\"application/ld\\+json\" data-camion=\"{marca}\">(.*?)</script>",
            RegexOptions.Singleline);
        Assert.True(m.Success, $"falta el JSON-LD {marca}");
        return JsonDocument.Parse(m.Groups[1].Value).RootElement;
    }

    [Fact(DisplayName = "CA-P10-71, 73, 75: ficha visible con título, descripción, canonical, Open Graph, Twitter y JSON-LD; el resto del index.html igual")]
    public async Task Ficha_visible()
    {
        var r = await encendida.Api.Get("/vehiculo/chevrolet-npr-2020");
        var html = await Html(r);
        const string titulo = "Chevrolet NPR 2020 en Medellín | Financiamos tu Camión";
        const string descripcion = "Camión Chevrolet NPR 2020, 85.000 km en Medellín, Antioquia. Precio: $ 120.000.000. " +
                                   "Escríbenos por WhatsApp o solicita tu crédito.";
        Assert.Equal(titulo, Titulo(html));
        Assert.Equal(descripcion, Meta(html, "description"));
        Assert.Contains($"<link rel=\"canonical\" href=\"{Url}/vehiculo/chevrolet-npr-2020\" data-camion=\"canonical\" />", html);
        Assert.Equal("website", Meta(html, "og:type"));
        Assert.Equal("Venta de Camiones – Financiamos tu Camión", Meta(html, "og:site_name"));
        Assert.Equal("es_CO", Meta(html, "og:locale"));
        Assert.Equal(titulo, Meta(html, "og:title"));
        Assert.Equal(descripcion, Meta(html, "og:description"));
        Assert.Equal($"{Url}/vehiculo/chevrolet-npr-2020", Meta(html, "og:url"));
        Assert.Equal($"{Url}/media/pub/v1/frente.jpg", Meta(html, "og:image"));
        Assert.Equal("Chevrolet NPR 2020 furgón", Meta(html, "og:image:alt"));
        Assert.Equal("summary_large_image", Meta(html, "twitter:card"));
        Assert.Equal(titulo, Meta(html, "twitter:title"));
        Assert.Equal(descripcion, Meta(html, "twitter:description"));
        Assert.Equal($"{Url}/media/pub/v1/frente.jpg", Meta(html, "twitter:image"));
        Assert.DoesNotContain("name=\"robots\"", html);
        Assert.False(r.Headers.Contains("X-Robots-Tag"));

        // CA-P10-75: solo cambia el bloque; lo de antes y después (scripts y estilos con huella) queda igual.
        Assert.StartsWith(PlantillaPrueba.Antes + PlantillaPortal.MarcaInicio, html);
        Assert.EndsWith(PlantillaPortal.MarcaFin + PlantillaPrueba.Despues, html);
        Assert.DoesNotContain("Descripción genérica ficticia.", html);

        // CA-P10-64, 65: Product + Vehicle.
        var v = JsonLd(html, FichaSeo.DataJsonLdVehiculo);
        Assert.Equal("https://schema.org", v.GetProperty("@context").GetString());
        Assert.Equal(new[] { "Product", "Vehicle" }, v.GetProperty("@type").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal("Chevrolet NPR 2020 en Medellín", v.GetProperty("name").GetString());
        Assert.Equal($"{Url}/vehiculo/chevrolet-npr-2020", v.GetProperty("url").GetString());
        Assert.Equal(new[] { $"{Url}/media/pub/v1/frente.jpg", $"{Url}/media/pub/v1/lateral.jpg", $"{Url}/media/pub/v1/cabina.jpg" },
            v.GetProperty("image").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal("Camión en buen estado, listo para trabajar.", v.GetProperty("description").GetString());
        Assert.Equal("Camión", v.GetProperty("category").GetString());
        Assert.Equal("Chevrolet", v.GetProperty("brand").GetProperty("name").GetString());
        Assert.Equal("NPR", v.GetProperty("model").GetString());
        Assert.Equal("2020", v.GetProperty("vehicleModelDate").GetString());
        Assert.Equal(85000, v.GetProperty("mileageFromOdometer").GetProperty("value").GetInt32());
        Assert.Equal("KMT", v.GetProperty("mileageFromOdometer").GetProperty("unitCode").GetString());
        Assert.Equal("Diésel", v.GetProperty("fuelType").GetString());
        var oferta = v.GetProperty("offers");
        Assert.Equal(120000000, oferta.GetProperty("price").GetInt64());
        Assert.Equal("COP", oferta.GetProperty("priceCurrency").GetString());
        Assert.Equal("https://schema.org/InStock", oferta.GetProperty("availability").GetString());
        var direccion = oferta.GetProperty("availableAtOrFrom").GetProperty("address");
        Assert.Equal("Medellín", direccion.GetProperty("addressLocality").GetString());
        Assert.Equal("Antioquia", direccion.GetProperty("addressRegion").GetString());
        Assert.Equal("CO", direccion.GetProperty("addressCountry").GetString());
        // CA-P10-66: sin vendedor, condición, placa ni WhatsApp.
        foreach (var prohibida in new[] { "seller", "itemCondition", "aggregateRating", "vehicleIdentificationNumber" })
            Assert.False(v.TryGetProperty(prohibida, out _), prohibida);

        // CA-P10-67: migas Inicio › Camiones › vehículo.
        var migas = JsonLd(html, FichaSeo.DataJsonLdMigas).GetProperty("itemListElement").EnumerateArray().ToArray();
        Assert.Equal(new[] { $"{Url}/", $"{Url}/camiones", $"{Url}/vehiculo/chevrolet-npr-2020" },
            migas.Select(m => m.GetProperty("item").GetString()).ToArray());
        Assert.Equal(new[] { "Inicio", "Camiones", "Chevrolet NPR 2020 en Medellín" },
            migas.Select(m => m.GetProperty("name").GetString()).ToArray());
        Assert.Equal(new[] { 1, 2, 3 }, migas.Select(m => m.GetProperty("position").GetInt32()).ToArray());
    }

    [Fact(DisplayName = "CA-P10-23, 65: reservado y negociable en la descripción; LimitedAvailability; portada de menor orden")]
    public async Task Ficha_reservada()
    {
        var html = await Html(await encendida.Api.Get("/vehiculo/kenworth-t800-2018"));
        Assert.Equal("Camión Kenworth T800 2018, 300.000 km en Medellín, Antioquia. Precio: $ 250.000.000 negociable. " +
                     "Reservado. Escríbenos por WhatsApp o solicita tu crédito.", Meta(html, "description"));
        Assert.Equal($"{Url}/media/pub/v2/a.jpg", Meta(html, "og:image"));
        var v = JsonLd(html, FichaSeo.DataJsonLdVehiculo);
        Assert.Equal("https://schema.org/LimitedAvailability", v.GetProperty("offers").GetProperty("availability").GetString());
        // Sin descripción comercial: la del meta.
        Assert.Equal(Meta(html, "description"), v.GetProperty("description").GetString());
    }

    [Fact(DisplayName = "CA-P10-22, 73: sin línea se usa el título; sin fotos, la imagen genérica; la URL de la foto va codificada")]
    public async Task Ficha_sin_linea_y_sin_fotos()
    {
        var v4 = await Html(await encendida.Api.Get("/vehiculo/chevrolet-camion-2015"));
        Assert.Equal("Chevrolet 2015 estacas en Medellín | Financiamos tu Camión", Titulo(v4));
        Assert.Equal($"{Url}/media/pub%201/foto%20%C3%B1.jpg", Meta(v4, "og:image"));
        Assert.False(JsonLd(v4, FichaSeo.DataJsonLdVehiculo).TryGetProperty("model", out _));

        var v3 = await Html(await encendida.Api.Get("/vehiculo/renault-logan-2021"));
        Assert.Equal("Renault Logan 2021 en Medellín | Financiamos tu Camión", Titulo(v3));
        Assert.Equal($"{Url}/marca/og-portal.jpg", Meta(v3, "og:image"));
        Assert.False(JsonLd(v3, FichaSeo.DataJsonLdVehiculo).TryGetProperty("image", out _));
        var migas = JsonLd(v3, FichaSeo.DataJsonLdMigas).GetProperty("itemListElement").EnumerateArray()
            .Select(m => m.GetProperty("item").GetString()).ToArray();
        Assert.Equal($"{Url}/carros", migas[1]);
    }

    [Fact(DisplayName = "CA-P10-23 (D-17): una carrocería no muestra kilometraje en la descripción ni en el JSON-LD")]
    public async Task Ficha_carroceria()
    {
        var html = await Html(await encendida.Api.Get("/vehiculo/carroceria-furgon-2019"));
        Assert.Equal("Furgón térmico en Medellín | Financiamos tu Camión", Titulo(html));
        Assert.Equal("Carrocería Carrocerías Andinas 2019 en Medellín, Antioquia. Precio: $ 100.000.000. " +
                     "Escríbenos por WhatsApp o solicita tu crédito.", Meta(html, "description"));
        Assert.False(JsonLd(html, FichaSeo.DataJsonLdVehiculo).TryGetProperty("mileageFromOdometer", out _));
        Assert.Equal($"{Url}/carrocerias",
            JsonLd(html, FichaSeo.DataJsonLdMigas).GetProperty("itemListElement")[1].GetProperty("item").GetString());
    }

    [Fact(DisplayName = "CA-P10-68, CA-P10-74: el texto del asesor se codifica; no se cierra el bloque ni aparece <img sin codificar")]
    public async Task Ficha_xss()
    {
        var html = await Html(await encendida.Api.Get("/vehiculo/xss-2020"));
        Assert.DoesNotContain("<img src=x", html);
        Assert.DoesNotContain("<script>alert", html);
        Assert.Equal(3, Regex.Matches(html, "</script>").Count); // 2 bloques JSON-LD + el script del frontend
        // El título pasa de 65 caracteres: va sin sufijo (CA-P10-31).
        Assert.Equal(PlantillaPrueba.TituloXss + " en Medellín", Titulo(html));
        Assert.Equal(PlantillaPrueba.TituloXss, Meta(html, "og:image:alt"));
        var v = JsonLd(html, FichaSeo.DataJsonLdVehiculo);
        Assert.Equal(PlantillaPrueba.TituloXss + " en Medellín", v.GetProperty("name").GetString());
        Assert.Equal("</script><script>alert(2)</script> & más", v.GetProperty("description").GetString());
    }

    [Fact(DisplayName = "CA-P10-72: inexistente, no visible, vacío, con barra o de más de 200 caracteres -> 404 con el mismo HTML genérico y noindex")]
    public async Task Ficha_no_disponible()
    {
        var esperado = PlantillaPrueba.Antes + PlantillaPortal.MarcaInicio + FichaSeo.BloqueNoDisponible(PlantillaPrueba.BloqueGenerico) +
                       PlantillaPortal.MarcaFin + PlantillaPrueba.Despues;
        Assert.Contains("<title>Venta de camiones usados y crédito | Financiamos tu Camión</title>", esperado);
        Assert.Contains(FichaSeo.MetaNoIndex, esperado);
        foreach (var ruta in new[] { "/vehiculo/n01-vendida", "/vehiculo/n14-plan-vencido", "/vehiculo/n08-asesor-bloqueado",
                     "/vehiculo/no-existe", "/vehiculo/", "/vehiculo/a/b", "/vehiculo/" + new string('a', 201),
                     "/vehiculo/%E0%A4%A" })
        {
            var r = await encendida.Api.Get(ruta);
            Assert.Equal(esperado, await Html(r, HttpStatusCode.NotFound));
            Assert.Equal("noindex", Assert.Single(r.Headers.GetValues("X-Robots-Tag")));
        }
    }

    [Fact(DisplayName = "CA-P10-53 (D-89 A): con la indexación apagada, X-Robots-Tag y meta robots noindex, sin canonical")]
    public async Task Ficha_indexacion_apagada()
    {
        var r = await apagada.Api.Get("/vehiculo/chevrolet-npr-2020");
        var html = await Html(r);
        Assert.Equal("noindex", Assert.Single(r.Headers.GetValues("X-Robots-Tag")));
        Assert.Contains(FichaSeo.MetaNoIndex, html);
        Assert.Single(Regex.Matches(html, "name=\"robots\""));
        Assert.DoesNotContain("rel=\"canonical\"", html);
        Assert.Equal($"{Url}/vehiculo/chevrolet-npr-2020", Meta(html, "og:url"));
    }

    [Fact(DisplayName = "CA-P10-76: el HTML lleva la CSP del frontend y los demás encabezados; las respuestas JSON siguen con la de la API")]
    public async Task Encabezados()
    {
        foreach (var ruta in new[] { "/vehiculo/chevrolet-npr-2020", "/vehiculo/no-existe" })
        {
            var r = await encendida.Api.Get(ruta);
            Assert.Equal(ConfiguracionServidor.CspFrontend, Assert.Single(r.Headers.GetValues("Content-Security-Policy")));
            Assert.Equal("no-referrer", Assert.Single(r.Headers.GetValues("Referrer-Policy")));
            Assert.Equal("nosniff", Assert.Single(r.Headers.GetValues("X-Content-Type-Options")));
            Assert.Equal("DENY", Assert.Single(r.Headers.GetValues("X-Frame-Options")));
            Assert.Equal("public, max-age=300", r.Headers.CacheControl!.ToString());
        }
        foreach (var ruta in new[] { "/api/catalogo/inicio", "/api/catalogo/publicaciones/chevrolet-npr-2020", "/robots.txt",
                     "/sitemap.xml", "/health" })
        {
            var r = await encendida.Api.Get(ruta);
            Assert.Equal("default-src 'none'; frame-ancestors 'none'", Assert.Single(r.Headers.GetValues("Content-Security-Policy")));
        }
    }

    [Fact(DisplayName = "CA-P10-84: la ficha no lleva datos del asesor, placa, WhatsApp ni ids internos")]
    public async Task Ficha_sin_datos_personales()
    {
        var s = encendida.Api.Datos;
        foreach (var slug in new[] { "chevrolet-npr-2020", "xss-2020" })
        {
            var html = await Html(await encendida.Api.Get("/vehiculo/" + slug));
            foreach (var dato in new[] { s.A1.Usuario.Email!, s.A1.Usuario.PhoneNumber!, s.A1.Usuario.NombreCompleto!,
                         s.A1.Perfil.NombreComercial!, "XYZ987", "ABC123", "wa.me", "whatsapp\":", "asesor", s.A1.Usuario.Id })
                Assert.DoesNotContain(dato, html, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact(DisplayName = "CA-P10-85: la ficha responde igual sin token, con token válido y con token no válido")]
    public async Task Ficha_igual_con_cualquier_token()
    {
        var api = encendida.Api;
        var sin = await (await api.Get("/vehiculo/chevrolet-npr-2020")).Content.ReadAsStringAsync();
        var a1 = api.Firmar(api.Datos.A1.Usuario.Id, api.Datos.A1.Usuario.SessionVersion, "ASESOR");
        foreach (var token in new[] { a1, "token-no-valido" })
        {
            var r = await api.Get("/vehiculo/chevrolet-npr-2020", token);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal(sin, await r.Content.ReadAsStringAsync());
        }
    }

    [Theory(DisplayName = "CA-P10-53, 72: con un index.html compilado con la indexación apagada (ya trae robots noindex) nunca hay dos robots")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Robots_sin_duplicar(bool indexacion)
    {
        // Como el bloque que deja frontend/etiquetasHtml.ts sin VITE_PERMITIR_INDEXACION=true.
        var bloque = PlantillaPrueba.BloqueGenerico.TrimEnd() +
                     "\n    <meta name=\"robots\" content=\"noindex\" data-camion=\"noindex\" />" +
                     "\n    <meta property=\"og:type\" content=\"website\" data-camion=\"og:type\" />\n    ";
        var ruta = PlantillaPrueba.Crear(PlantillaPrueba.Antes + PlantillaPortal.MarcaInicio + bloque + PlantillaPortal.MarcaFin +
                                         PlantillaPrueba.Despues);
        try
        {
            using var api = await CatalogoApi.Crear(configurar: b =>
            {
                b.UseSetting(ConfiguracionPortal.ClaveRutaIndexHtml, ruta);
                b.UseSetting(ConfiguracionPortal.ClavePermitirIndexacion, indexacion ? "true" : "false");
            });
            var ficha = await Html(await api.Get("/vehiculo/chevrolet-npr-2020"));
            Assert.Equal(indexacion ? 0 : 1, Regex.Matches(ficha, "name=\"robots\"").Count);
            foreach (var ruta404 in new[] { "/vehiculo/no-existe", "/vehiculo/n01-vendida" })
            {
                var html = await Html(await api.Get(ruta404), HttpStatusCode.NotFound);
                Assert.Single(Regex.Matches(html, "name=\"robots\""));
                Assert.Contains(FichaSeo.MetaNoIndex, html);
                // El resto del bloque genérico se conserva.
                Assert.Contains("<meta property=\"og:type\" content=\"website\" data-camion=\"og:type\" />", html);
                Assert.Contains("<title>Venta de camiones usados y crédito | Financiamos tu Camión</title>", html);
            }
        }
        finally
        {
            PlantillaPrueba.Borrar(ruta);
        }
    }

    [Fact(DisplayName = "CA-P10-70: sin Portal:RutaIndexHtml la API no atiende /vehiculo/ (404 vacío, no HTML)")]
    public async Task Sin_plantilla_no_hay_ruta()
    {
        using var api = await CatalogoApi.Crear(noVisibles: false);
        var r = await api.Get("/vehiculo/chevrolet-npr-2020");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Equal("", await r.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "CA-P10-75, B-2: sin las marcas, /vehiculo/ responde 503 (nunca el archivo tal cual) y se avisa en el log una vez")]
    public async Task Sin_marcas()
    {
        const string sinMarcas = "<!doctype html><html><head><title>Genérico</title></head><body></body></html>";
        var ruta = PlantillaPrueba.Crear(sinMarcas);
        try
        {
            using var api = await CatalogoApi.Crear(noVisibles: false,
                configurar: b => b.UseSetting(ConfiguracionPortal.ClaveRutaIndexHtml, ruta));
            foreach (var slug in new[] { "chevrolet-npr-2020", "no-existe" })
            {
                var r = await api.Get("/vehiculo/" + slug);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
                Assert.Equal("text/plain; charset=utf-8", r.Content.Headers.ContentType!.ToString());
                Assert.Equal(SeoEndpoints.MensajeNoDisponible, await r.Content.ReadAsStringAsync());
            }
            var aviso = Assert.Single(api.Base.Logs, l => l.Contains("|Warning|") && l.Contains(PlantillaPortal.MarcaInicio));
            Assert.DoesNotContain(ruta, aviso);

            // Al publicar un index.html con las marcas, vuelve a funcionar sin reiniciar.
            File.WriteAllText(ruta, PlantillaPrueba.Completa);
            File.SetLastWriteTimeUtc(ruta, DateTime.UtcNow.AddMinutes(5));
            Assert.Contains("Chevrolet NPR 2020 en Medellín", await Html(await api.Get("/vehiculo/chevrolet-npr-2020")));
        }
        finally
        {
            PlantillaPrueba.Borrar(ruta);
        }
    }

    [Fact(DisplayName = "CA-P10-77: una publicación nueva del frontend se ve sin reiniciar; si el archivo desaparece, 503 y error en el log")]
    public async Task Recarga_y_503()
    {
        var ruta = PlantillaPrueba.Crear();
        try
        {
            using var api = await CatalogoApi.Crear(noVisibles: false,
                configurar: b => b.UseSetting(ConfiguracionPortal.ClaveRutaIndexHtml, ruta));
            Assert.Contains("/assets/index-Ab12Cd.js", await Html(await api.Get("/vehiculo/chevrolet-npr-2020")));

            File.WriteAllText(ruta, PlantillaPrueba.Completa.Replace("index-Ab12Cd.js", "index-Nuevo99.js"));
            File.SetLastWriteTimeUtc(ruta, DateTime.UtcNow.AddMinutes(5));
            var nuevo = await Html(await api.Get("/vehiculo/chevrolet-npr-2020"));
            Assert.Contains("/assets/index-Nuevo99.js", nuevo);
            Assert.DoesNotContain("index-Ab12Cd.js", nuevo);

            File.Delete(ruta);
            var r = await api.Get("/vehiculo/chevrolet-npr-2020");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
            Assert.Equal("text/plain; charset=utf-8", r.Content.Headers.ContentType!.ToString());
            Assert.Equal(SeoEndpoints.MensajeNoDisponible, await r.Content.ReadAsStringAsync());
            var error = Assert.Single(api.Base.Logs, l => l.Contains("|Error|") && l.Contains(ConfiguracionPortal.ClaveRutaIndexHtml));
            Assert.DoesNotContain(ruta, error);
        }
        finally
        {
            PlantillaPrueba.Borrar(ruta);
        }
    }

    [Fact(DisplayName = "CA-P10-77: fuera de Development, con Portal:RutaIndexHtml y sin el archivo, la API no arranca (sin la ruta en el mensaje)")]
    public void Sin_archivo_no_arranca()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"camion_no_existe_{Guid.NewGuid():N}", "index.html");
        using var f = new CamionApiFactory();
        using var w = f.WithWebHostBuilder(b => b.UseSetting(ConfiguracionPortal.ClaveRutaIndexHtml, ruta));
        var ex = Assert.ThrowsAny<Exception>(() => w.CreateClient());
        var mensaje = (ex.InnerException ?? ex).Message + ex.Message;
        Assert.Contains(ConfiguracionPortal.ClaveRutaIndexHtml, mensaje);
        Assert.DoesNotContain(ruta, mensaje);
    }
}

/// <summary>P10: reglas de configuración y de texto sin levantar la API (CA-P10-22, 23, 31, 58).</summary>
public sealed class P10ReglasTests
{
    private static IConfiguration Config(params (string Clave, string? Valor)[] valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores.Select(v => new KeyValuePair<string, string?>(v.Clave, v.Valor)))
            .Build();

    [Fact(DisplayName = "CA-P10-58: URL pública desde Portal:UrlPublica o Recuperacion:UrlPortal, sin barra final; https fuera de Development")]
    public void Url_publica()
    {
        Assert.Equal("http://localhost:5173", ConfiguracionPortal.UrlPublica(Config(), esDesarrollo: true));
        Assert.Equal("https://a.test", ConfiguracionPortal.UrlPublica(Config(("Recuperacion:UrlPortal", "https://a.test/")), false));
        Assert.Equal("https://b.test", ConfiguracionPortal.UrlPublica(
            Config(("Recuperacion:UrlPortal", "https://a.test"), ("Portal:UrlPublica", " https://b.test/ ")), false));
        Assert.Equal("https://b.test:8443", ConfiguracionPortal.UrlPublica(Config(("Portal:UrlPublica", "https://b.test:8443")), false));
        // Portal:Origen (CORS) no se usa.
        Assert.Equal("https://a.test", ConfiguracionPortal.UrlPublica(
            Config(("Recuperacion:UrlPortal", "https://a.test"), ("Portal:Origen", "https://otro.test")), false));

        foreach (var malo in new[] { "https://b.test/portal", "https://b.test/?x=1", "https://b.test/#x", "ftp://b.test",
                     "b.test", "https://usuario:clave@b.test" })
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                ConfiguracionPortal.UrlPublica(Config(("Portal:UrlPublica", malo)), true));
            Assert.Contains("Portal:UrlPublica", ex.Message);
            Assert.DoesNotContain(malo, ex.Message);
        }
        Assert.Contains("https://", Assert.Throws<InvalidOperationException>(() =>
            ConfiguracionPortal.UrlPublica(Config(("Portal:UrlPublica", "http://b.test")), false)).Message);
        Assert.Contains("Recuperacion:UrlPortal", Assert.Throws<InvalidOperationException>(() =>
            ConfiguracionPortal.UrlPublica(Config(("Recuperacion:UrlPortal", "http://a.test")), false)).Message);
        Assert.Equal("http://b.test", ConfiguracionPortal.UrlPublica(Config(("Portal:UrlPublica", "http://b.test")), true));
    }

    [Fact(DisplayName = "D-89 A: Portal:PermitirIndexacion es false por defecto; un valor que no sea true/false impide arrancar")]
    public void Permitir_indexacion()
    {
        Assert.False(ConfiguracionPortal.PermitirIndexacion(null));
        Assert.False(ConfiguracionPortal.PermitirIndexacion(" "));
        Assert.False(ConfiguracionPortal.PermitirIndexacion("false"));
        Assert.True(ConfiguracionPortal.PermitirIndexacion("True"));
        Assert.Contains("Portal:PermitirIndexacion",
            Assert.Throws<InvalidOperationException>(() => ConfiguracionPortal.PermitirIndexacion("si")).Message);
    }

    [Fact(DisplayName = "CA-P10-70: Portal:RutaIndexHtml vacía desactiva la ruta; relativa se resuelve desde la carpeta de la API")]
    public void Ruta_index()
    {
        Assert.Null(ConfiguracionPortal.RutaIndexHtml(null, false, Path.GetTempPath()));
        Assert.Null(ConfiguracionPortal.RutaIndexHtml("  ", false, Path.GetTempPath()));
        var raiz = Path.Combine(Path.GetTempPath(), "camion_raiz_ficticia");
        // En Development no se exige que exista (se responde 503 hasta compilar el frontend).
        Assert.Equal(Path.GetFullPath(Path.Combine(raiz, "..", "frontend", "dist", "index.html")),
            ConfiguracionPortal.RutaIndexHtml("../frontend/dist/index.html", true, raiz));
        Assert.Throws<InvalidOperationException>(() =>
            ConfiguracionPortal.RutaIndexHtml("../frontend/dist/index.html", false, raiz));
    }

    private static DatosFicha Datos(string titulo = "Título", string? marca = "Chevrolet", string? linea = "NPR", int? anio = 2018,
        string municipio = "Medellín", string tipo = "Camión", int km = 0) =>
        new("slug-1", titulo, tipo, marca, linea, anio, km, municipio, "Antioquia", 1_000_000, false, "DISPONIBLE", null, null,
            null, []);

    [Fact(DisplayName = "CA-P10-22, CA-P10-31: título con marca, línea, año y ciudad; sin año se omite; si pasa de 65, sin sufijo")]
    public void Titulos()
    {
        Assert.Equal("Chevrolet NPR 2018 en Medellín | Financiamos tu Camión", FichaSeo.TituloPagina(Datos()));
        Assert.Equal("Chevrolet NPR en Medellín | Financiamos tu Camión", FichaSeo.TituloPagina(Datos(anio: null)));
        Assert.Equal("Volqueta doble troque en Medellín | Financiamos tu Camión",
            FichaSeo.TituloPagina(Datos(titulo: "Volqueta doble troque", marca: null)));
        var largo = Datos(titulo: "Carrocería de estacas para camión sencillo", marca: null, municipio: "San José del Guaviare");
        Assert.Equal("Carrocería de estacas para camión sencillo en San José del Guaviare", FichaSeo.TituloPagina(largo));
    }

    [Fact(DisplayName = "CA-P10-72: SinRobots quita cualquier meta robots (comillas, mayúsculas, sin barra) y deja lo demás")]
    public void Sin_robots()
    {
        const string bloque = "\n    <title>T</title>\n    <meta name=\"robots\" content=\"noindex\" data-camion=\"noindex\" />\n" +
                              "    <META NAME='robots' content='noindex,follow'>\n    <meta name=\"robotsx\" content=\"x\" />\n" +
                              "    <meta name=\"description\" content=\"robots\" />\n    ";
        Assert.Equal("\n    <title>T</title>\n    <meta name=\"robotsx\" content=\"x\" />\n" +
                     "    <meta name=\"description\" content=\"robots\" />\n    ", FichaSeo.SinRobots(bloque));
    }

    [Fact(DisplayName = "CA-P10-67, D-17: nombres de categoría de las migas iguales al frontend (CATEGORIAS.enlace) desde los tipos de la base")]
    public void Categorias_migas()
    {
        // Nombres de tipos_vehiculo en camion_ventas (SELECT de solo lectura): Carro, Camión, Carrocería, Maquinaria.
        Assert.Equal(new[] { "Carros", "Camiones", "Carrocerías", "Maquinaria" },
            new[] { "Carro", "Camión", "Carrocería", "Maquinaria" }.Select(t => Categorias.Ruta(t)!.Nombre).ToArray());
        Assert.Equal(new[] { "/carros", "/camiones", "/carrocerias", "/maquinaria" },
            new[] { "CARRO", " camion ", "Carroceria", "maquinaria" }.Select(t => Categorias.Ruta(t)!.Ruta).ToArray());
        Assert.Null(Categorias.Ruta("Remolque"));
    }

    [Fact(DisplayName = "CA-P10-23: descripción cortada en 160 caracteres por palabra completa con …; maquinaria en 0 km sin kilometraje")]
    public void Descripciones()
    {
        Assert.Equal("Maquinaria Caterpillar 320 2018 en Medellín, Antioquia. Precio: $ 1.000.000. " +
                     "Escríbenos por WhatsApp o solicita tu crédito.",
            FichaSeo.Descripcion(Datos(tipo: "Maquinaria", marca: "Caterpillar", linea: "320")));
        Assert.Contains(", 1.234.567 km en", FichaSeo.Descripcion(Datos(km: 1_234_567)));
        var larga = FichaSeo.Descripcion(Datos(marca: new string('M', 30), linea: new string('L', 40), municipio: "Municipio ficticio largo"));
        Assert.True(larga.Length <= 160, $"{larga.Length}: {larga}");
        Assert.EndsWith("…", larga);
        Assert.Equal("uno dos…", FichaSeo.Recortar("uno dos tres", 10));
        Assert.Equal("uno dos tres", FichaSeo.Recortar("uno dos tres", 12));
    }
}
