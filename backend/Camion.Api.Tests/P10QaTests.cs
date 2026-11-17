using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Camion.Api.Seo;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Hosting;

namespace Camion.Api.Tests;

/// <summary>
/// QA P10: prueba que solo corre si las variables de entorno apuntan a un index.html compilado de verdad con
/// <c>npm run build</c>. Si faltan, la prueba aparece como OMITIDA con el motivo (nunca pasa en vacío).
/// </summary>
public sealed class FactConIndexCompiladoAttribute : FactAttribute
{
    public FactConIndexCompiladoAttribute(params string[] variables)
    {
        var faltan = variables.Where(v => Environment.GetEnvironmentVariable(v) is not { Length: > 0 } ruta || !File.Exists(ruta))
            .ToArray();
        if (faltan.Length > 0)
            Skip = $"Falta {string.Join(" y ", faltan)}: compila el frontend en una carpeta temporal y apunta la variable a su " +
                   "index.html (docs/criterios/P10-evidencia.md §1).";
    }
}

/// <summary>
/// QA P10 (D-88 A, D-89 A): /vehiculo/{slug} con el <c>dist/index.html</c> REAL de <c>npm run build</c> como
/// Portal:RutaIndexHtml (CA-P10-53, 68, 71 a 76, 82). Dos compilaciones:
/// <list type="bullet">
/// <item><c>CAMION_QA_INDEX_APAGADA</c>: sin variables (trae robots noindex en el bloque).</item>
/// <item><c>CAMION_QA_INDEX_ENCENDIDA</c>: VITE_PERMITIR_INDEXACION=true y VITE_URL_PUBLICA=https://ejemplo.test (trae og:url,
/// og:image y twitter:image genéricos absolutos).</item>
/// </list>
/// SQLite en memoria (CatalogoApi); nunca camion_ventas. El archivo compilado solo se lee.
/// </summary>
public sealed class P10QaIndexCompiladoTests
{
    public const string VarApagada = "CAMION_QA_INDEX_APAGADA";
    public const string VarEncendida = "CAMION_QA_INDEX_ENCENDIDA";
    private const string UrlEjemplo = "https://ejemplo.test";
    private const string UrlPruebas = CamionApiFactory.UrlPortalPruebas;

    private static string Ruta(string variable) => Environment.GetEnvironmentVariable(variable)!;

    private static Task<CatalogoApi> Api(string variable, bool indexar) => CatalogoApi.Crear(extra: PlantillaPrueba.Sembrar,
        configurar: b =>
        {
            b.UseSetting(ConfiguracionPortal.ClaveRutaIndexHtml, Ruta(variable));
            b.UseSetting(ConfiguracionPortal.ClavePermitirIndexacion, indexar ? "true" : "false");
            if (indexar) b.UseSetting(ConfiguracionPortal.ClaveUrlPublica, UrlEjemplo);
        });

    private static async Task<string> Html(HttpResponseMessage r, HttpStatusCode esperado)
    {
        var texto = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == esperado, $"se esperaba {(int)esperado} y llegó {(int)r.StatusCode}");
        Assert.Equal("text/html; charset=utf-8", r.Content.Headers.ContentType!.ToString());
        return texto;
    }

    private static int Contar(string html, string patron) => Regex.Matches(html, patron, RegexOptions.IgnoreCase).Count;

    /// <summary>Valor (decodificado) de la única etiqueta con ese name/property; falla si hay 0 o más de 1.</summary>
    private static string Meta(string html, string nombre)
    {
        var m = Regex.Matches(html, $"<meta (?:name|property)=\"{Regex.Escape(nombre)}\" content=\"([^\"]*)\"");
        Assert.True(m.Count == 1, $"{nombre}: {m.Count} etiquetas");
        return WebUtility.HtmlDecode(m[0].Groups[1].Value);
    }

    /// <summary>CA-P10-28: una sola etiqueta de cada tipo en todo el documento.</summary>
    private static void UnaDeCada(string html, string contexto)
    {
        Assert.True(Contar(html, "<title>") == 1, $"{contexto}: <title> x{Contar(html, "<title>")}");
        Assert.True(Contar(html, "<meta name=\"description\"") == 1, $"{contexto}: description");
        Assert.True(Contar(html, "<meta name=\"robots\"") <= 1, $"{contexto}: robots x{Contar(html, "<meta name=\"robots\"")}");
        Assert.True(Contar(html, "rel=\"canonical\"") <= 1, $"{contexto}: canonical");
        foreach (var grupo in Regex.Matches(html, "<meta (?:name|property)=\"((?:og|twitter):[^\"]+)\"").GroupBy(m => m.Groups[1].Value))
            Assert.True(grupo.Count() == 1, $"{contexto}: {grupo.Key} x{grupo.Count()}");
        foreach (var grupo in Regex.Matches(html, "<script type=\"application/ld\\+json\" data-camion=\"([^\"]+)\"").GroupBy(m => m.Groups[1].Value))
            Assert.True(grupo.Count() == 1, $"{contexto}: JSON-LD {grupo.Key} x{grupo.Count()}");
    }

    /// <summary>CA-P10-75: fuera del bloque, el documento es el index.html compilado byte a byte (scripts y estilos con huella).</summary>
    private static void FueraDelBloqueIgual(string html, string rutaIndex)
    {
        var original = PlantillaPortal.Partir(DateTime.MinValue, 0, File.ReadAllText(rutaIndex));
        Assert.NotNull(original.Bloque);
        Assert.StartsWith(original.Antes + PlantillaPortal.MarcaInicio, html);
        Assert.EndsWith(PlantillaPortal.MarcaFin + original.Despues, html);
        Assert.Matches("<script type=\"module\" crossorigin src=\"/assets/index-[A-Za-z0-9_-]+\\.js\"></script>", original.Despues);
        Assert.Matches("<link rel=\"stylesheet\" crossorigin href=\"/assets/index-[A-Za-z0-9_-]+\\.css\">", original.Despues);
    }

    /// <summary>CA-P10-76: con la CSP del frontend (script-src = 'self') el HTML no puede depender de scripts en línea.</summary>
    private static void SinScriptsEnLinea(string html)
    {
        foreach (Match m in Regex.Matches(html, "<script\\b([^>]*)>(.*?)</script>", RegexOptions.Singleline))
        {
            var atributos = m.Groups[1].Value;
            Assert.True(atributos.Contains("type=\"application/ld+json\"") || (atributos.Contains("src=\"/assets/") && m.Groups[2].Value.Length == 0),
                $"script no permitido por la CSP del frontend: <script{atributos}>");
        }
    }

    private static JsonElement JsonLd(string html, string marca)
    {
        var m = Regex.Match(html, $"<script type=\"application/ld\\+json\" data-camion=\"{marca}\">(.*?)</script>", RegexOptions.Singleline);
        Assert.True(m.Success, $"falta el JSON-LD {marca}");
        Assert.DoesNotContain("<", m.Groups[1].Value);
        return JsonDocument.Parse(m.Groups[1].Value).RootElement;
    }

    [FactConIndexCompilado(VarApagada)]
    public async Task Compilado_apagado_ficha_visible()
    {
        using var api = await Api(VarApagada, indexar: false);
        var r = await api.Get("/vehiculo/chevrolet-npr-2020");
        var html = await Html(r, HttpStatusCode.OK);
        UnaDeCada(html, "apagada");
        FueraDelBloqueIgual(html, Ruta(VarApagada));
        SinScriptsEnLinea(html);
        // D-89 A: un solo robots noindex (el del bloque compilado se reemplazó, no se sumó), sin canonical.
        Assert.Equal(1, Contar(html, "<meta name=\"robots\" content=\"noindex\""));
        Assert.Equal(0, Contar(html, "rel=\"canonical\""));
        Assert.Equal("noindex", Assert.Single(r.Headers.GetValues("X-Robots-Tag")));
        Assert.Equal("Chevrolet NPR 2020 en Medellín | Financiamos tu Camión", WebUtility.HtmlDecode(
            Regex.Match(html, "<title>([^<]*)</title>").Groups[1].Value));
        Assert.Equal($"{UrlPruebas}/media/pub/v1/frente.jpg", Meta(html, "og:image"));
        Assert.Equal($"{UrlPruebas}/media/pub/v1/frente.jpg", Meta(html, "twitter:image"));
        Assert.Equal($"{UrlPruebas}/vehiculo/chevrolet-npr-2020", Meta(html, "og:url"));
        // Nada del bloque genérico de la portada (título y descripción del portal).
        Assert.DoesNotContain("Venta de camiones usados y crédito", html);
        Assert.Equal("Chevrolet NPR 2020 en Medellín", JsonLd(html, FichaSeo.DataJsonLdVehiculo).GetProperty("name").GetString());
        Assert.Equal("BreadcrumbList", JsonLd(html, FichaSeo.DataJsonLdMigas).GetProperty("@type").GetString());
    }

    [FactConIndexCompilado(VarEncendida)]
    public async Task Compilado_encendido_ficha_visible()
    {
        using var api = await Api(VarEncendida, indexar: true);
        var r = await api.Get("/vehiculo/chevrolet-npr-2020");
        var html = await Html(r, HttpStatusCode.OK);
        UnaDeCada(html, "encendida");
        FueraDelBloqueIgual(html, Ruta(VarEncendida));
        SinScriptsEnLinea(html);
        Assert.Equal(0, Contar(html, "<meta name=\"robots\""));
        Assert.False(r.Headers.Contains("X-Robots-Tag"));
        Assert.Contains($"<link rel=\"canonical\" href=\"{UrlEjemplo}/vehiculo/chevrolet-npr-2020\" data-camion=\"canonical\" />", html);
        // og:url, og:image y twitter:image del vehículo reemplazan a los genéricos absolutos del compilado (no se suman).
        Assert.Equal($"{UrlEjemplo}/vehiculo/chevrolet-npr-2020", Meta(html, "og:url"));
        Assert.Equal($"{UrlEjemplo}/media/pub/v1/frente.jpg", Meta(html, "og:image"));
        Assert.Equal($"{UrlEjemplo}/media/pub/v1/frente.jpg", Meta(html, "twitter:image"));
        Assert.DoesNotContain($"content=\"{UrlEjemplo}/\"", html);
        Assert.DoesNotContain("og-portal.jpg", html);
        var v = JsonLd(html, FichaSeo.DataJsonLdVehiculo);
        Assert.Equal($"{UrlEjemplo}/vehiculo/chevrolet-npr-2020", v.GetProperty("url").GetString());
        Assert.All(v.GetProperty("image").EnumerateArray(), i => Assert.StartsWith($"{UrlEjemplo}/media/", i.GetString()));

        // Sin fotos: la imagen genérica con la URL pública, una sola vez.
        var sinFotos = await Html(await api.Get("/vehiculo/renault-logan-2021"), HttpStatusCode.OK);
        UnaDeCada(sinFotos, "sin fotos");
        Assert.Equal($"{UrlEjemplo}/marca/og-portal.jpg", Meta(sinFotos, "og:image"));
        Assert.Equal($"{UrlEjemplo}/marca/og-portal.jpg", Meta(sinFotos, "twitter:image"));
    }

    [FactConIndexCompilado(VarApagada, VarEncendida)]
    public async Task Compilado_xss_en_titulo_y_descripcion()
    {
        foreach (var (variable, indexar) in new[] { (VarApagada, false), (VarEncendida, true) })
        {
            using var api = await Api(variable, indexar);
            var html = await Html(await api.Get("/vehiculo/xss-2020"), HttpStatusCode.OK);
            UnaDeCada(html, variable);
            SinScriptsEnLinea(html);
            Assert.DoesNotContain("<img src=x", html);
            Assert.DoesNotContain("<script>alert", html);
            // 2 JSON-LD + el módulo de Vite; ninguno más.
            Assert.Equal(3, Contar(html, "<script\\b"));
            Assert.Equal(3, Contar(html, "</script>"));
            Assert.Equal(PlantillaPrueba.TituloXss, Meta(html, "og:image:alt"));
            Assert.Equal(PlantillaPrueba.TituloXss + " en Medellín", Meta(html, "og:title"));
            Assert.Equal(PlantillaPrueba.TituloXss + " en Medellín", Meta(html, "twitter:title"));
            var v = JsonLd(html, FichaSeo.DataJsonLdVehiculo);
            Assert.Equal(PlantillaPrueba.TituloXss + " en Medellín", v.GetProperty("name").GetString());
            Assert.Equal("</script><script>alert(2)</script> & más", v.GetProperty("description").GetString());
            var migas = JsonLd(html, FichaSeo.DataJsonLdMigas);
            Assert.Equal(PlantillaPrueba.TituloXss + " en Medellín", migas.GetProperty("itemListElement")[2].GetProperty("name").GetString());
        }
    }

    /// <summary>Slugs que no son una ficha visible: mismo cuerpo 404, un solo robots, un solo title y el slug no se refleja.</summary>
    public static readonly string[] SlugsRaros =
    [
        "/vehiculo/n01-vendida", "/vehiculo/no-existe", "/vehiculo/..%2F..%2Fapi%2Fcatalogo%2Ffiltros",
        "/vehiculo/%22%3E%3Cimg%20src%3Dx%20onerror%3Dalert(1)%3E", "/vehiculo/%3Cscript%3Ealert(1)%3C%2Fscript%3E",
        "/vehiculo/" + new string('a', 201), "/vehiculo/" + new string('b', 5000), "/vehiculo/%E0%A4%A", "/vehiculo/a/b",
        "/vehiculo/CHEVROLET-NPR-2020x"
        // "%00" no se prueba aquí: TestServer lanza "The path contains null characters" antes de llegar a la API (Kestrel
        // responde 400 por su cuenta).
    ];

    [FactConIndexCompilado(VarApagada, VarEncendida)]
    public async Task Compilado_no_disponible_y_slugs_raros()
    {
        foreach (var (variable, indexar) in new[] { (VarApagada, false), (VarEncendida, true) })
        {
            using var api = await Api(variable, indexar);
            string? primero = null;
            foreach (var ruta in SlugsRaros)
            {
                var r = await api.Get(ruta);
                var html = await Html(r, HttpStatusCode.NotFound);
                UnaDeCada(html, $"{variable} {ruta[..Math.Min(ruta.Length, 60)]}");
                FueraDelBloqueIgual(html, Ruta(variable));
                Assert.Equal(1, Contar(html, "<meta name=\"robots\" content=\"noindex\""));
                Assert.Equal("noindex", Assert.Single(r.Headers.GetValues("X-Robots-Tag")));
                Assert.Contains("<title>Venta de camiones usados y crédito | Financiamos tu Camión</title>", html);
                Assert.DoesNotContain("<img src=x", html);
                Assert.DoesNotContain("<script>alert", html);
                Assert.DoesNotContain("..%2F", html);
                Assert.DoesNotContain("bbbbbbbbbb", html);
                Assert.Equal(0, Contar(html, "application/ld\\+json"));
                primero ??= html;
                Assert.Equal(primero, html);
            }
            // El 404 conserva las etiquetas genéricas del compilado: con URL pública, og:image genérica una sola vez.
            if (indexar) Assert.Equal($"{UrlEjemplo}/marca/og-portal.jpg", Meta(primero!, "og:image"));
            else Assert.DoesNotContain("og:image", primero);
        }
    }

    [FactConIndexCompilado(VarApagada)]
    public async Task Compilado_head()
    {
        using var api = await Api(VarApagada, indexar: false);
        foreach (var (ruta, estado) in new[] { ("/vehiculo/chevrolet-npr-2020", HttpStatusCode.OK), ("/vehiculo/no-existe", HttpStatusCode.NotFound) })
        {
            var r = await api.Cliente.SendAsync(new HttpRequestMessage(HttpMethod.Head, ruta));
            Assert.Equal(estado, r.StatusCode);
            Assert.Equal("text/html; charset=utf-8", r.Content.Headers.ContentType!.ToString());
            Assert.Equal("noindex", Assert.Single(r.Headers.GetValues("X-Robots-Tag")));
            // El cuerpo de HEAD no se comprueba: TestServer lo deja pasar y Kestrel lo descarta por su cuenta.
        }
    }
}

/// <summary>QA P10 (CA-P10-46, 48, 70, 87): con la plantilla configurada, la API nunca responde el index.html fuera de /vehiculo/.</summary>
public sealed class P10QaPrefijosTests
{
    [Fact(DisplayName = "QA CA-P10-48: /api, /media, /health, /assets, archivos y rutas del SPA no reciben el index.html desde la API")]
    public async Task Prefijos_no_caen_en_el_spa()
    {
        var plantilla = PlantillaPrueba.Crear();
        try
        {
            using var api = await CatalogoApi.Crear(configurar: b =>
            {
                b.UseSetting(ConfiguracionPortal.ClaveRutaIndexHtml, plantilla);
                b.UseSetting(ConfiguracionPortal.ClavePermitirIndexacion, "true");
            });
            foreach (var ruta in new[]
                     {
                         "/api/no-existe", "/api/catalogo/publicaciones/no-existe", "/api/vehiculo/chevrolet-npr-2020",
                         "/media/no-existe.jpg", "/media/vehiculo/chevrolet-npr-2020", "/assets/index-x.js", "/assets/vehiculo/x.css",
                         "/marca/og-portal.jpg", "/favicon.ico", "/site.webmanifest", "/vehiculos", "/camiones",
                         "/restablecer-contrasena", "/cuenta/usuarios", "/index.html", "/vehiculos/chevrolet-npr-2020"
                     })
            {
                var r = await api.Get(ruta);
                var cuerpo = await r.Content.ReadAsStringAsync();
                Assert.True(r.StatusCode == HttpStatusCode.NotFound, $"{ruta}: {(int)r.StatusCode}");
                Assert.False(r.Content.Headers.ContentType?.MediaType == "text/html", $"{ruta}: devolvió HTML");
                Assert.DoesNotContain(PlantillaPortal.MarcaInicio, cuerpo);
                Assert.DoesNotContain("<html", cuerpo);
            }
            // /api/… sigue con su 404 JSON (o vacío) y la CSP de la API.
            var apiNoExiste = await api.Get("/api/catalogo/publicaciones/no-existe");
            Assert.Equal("application/json", apiNoExiste.Content.Headers.ContentType?.MediaType);
            Assert.Equal("default-src 'none'; frame-ancestors 'none'", Assert.Single(apiNoExiste.Headers.GetValues("Content-Security-Policy")));
            var salud = await api.Get("/health");
            Assert.Equal(HttpStatusCode.OK, salud.StatusCode);
            Assert.NotEqual("text/html", salud.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            PlantillaPrueba.Borrar(plantilla);
        }
    }

    [Fact(DisplayName = "QA CA-P10-72: /vehiculo/{slug}/ redirige 301 sin la barra y /Vehiculo/ da 404 noindex, como el SPA (O-1 y O-2 corregidos)")]
    public async Task Barra_final()
    {
        var plantilla = PlantillaPrueba.Crear();
        try
        {
            using var api = await CatalogoApi.Crear(configurar: b =>
            {
                b.UseSetting(ConfiguracionPortal.ClaveRutaIndexHtml, plantilla);
                b.UseSetting(ConfiguracionPortal.ClavePermitirIndexacion, "true");
            });
            // O-1 (corregido): una barra final redirige 301 a la dirección sin barra (ver P10SeguridadTests.Barra_final_redirige).
            using var sinSeguir = api.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
                { AllowAutoRedirect = false });
            var r = await sinSeguir.GetAsync("/vehiculo/chevrolet-npr-2020/");
            Assert.Equal(HttpStatusCode.MovedPermanently, r.StatusCode);
            Assert.Equal("/vehiculo/chevrolet-npr-2020", r.Headers.Location!.OriginalString);
            // O-2 (corregido): /Vehiculo/<slug> da el 404 genérico con noindex, como el SPA (CA-P10-34).
            var mayusculas = await api.Get("/Vehiculo/chevrolet-npr-2020");
            Assert.Equal(HttpStatusCode.NotFound, mayusculas.StatusCode);
            Assert.Equal("noindex", Assert.Single(mayusculas.Headers.GetValues("X-Robots-Tag")));
            Assert.DoesNotContain("rel=\"canonical\"", await mayusculas.Content.ReadAsStringAsync());
            // Sin slug ("/vehiculo"): la ruta comodín también responde el 404 HTML genérico, nunca la portada con 200.
            var sinSlug = await api.Get("/vehiculo");
            Assert.NotEqual(HttpStatusCode.OK, sinSlug.StatusCode);
        }
        finally
        {
            PlantillaPrueba.Borrar(plantilla);
        }
    }
}

/// <summary>QA P10 (CA-P10-89, medición orientativa): 1.000 publicaciones visibles en SQLite en memoria.</summary>
public sealed class P10QaRendimientoTests(Xunit.Abstractions.ITestOutputHelper salida)
{
    [Fact(DisplayName = "QA CA-P10-89: sitemap.xml con 1.000 publicaciones visibles y /vehiculo/{slug} responden en menos de 500 ms")]
    public async Task Sitemap_y_ficha_con_mil()
    {
        var plantilla = PlantillaPrueba.Crear();
        try
        {
            using var api = await CatalogoApi.Crear(noVisibles: false, extra: s =>
            {
                for (var i = 0; i < 1000; i++) s.Pub($"qa-masivo-{i:0000}");
            }, configurar: b =>
            {
                b.UseSetting(ConfiguracionPortal.ClaveRutaIndexHtml, plantilla);
                b.UseSetting(ConfiguracionPortal.ClavePermitirIndexacion, "true");
            });
            // Primera llamada de calentamiento (compilación de consultas); luego la mejor de 3.
            var r0 = await api.Get("/sitemap.xml");
            var xml = await r0.Content.ReadAsStringAsync();
            Assert.Equal(1000, Regex.Matches(xml, "/vehiculo/qa-masivo-").Count);
            await api.Get("/vehiculo/qa-masivo-0500");

            async Task<long> Mejor(string ruta, HttpStatusCode esperado)
            {
                var mejor = long.MaxValue;
                for (var i = 0; i < 3; i++)
                {
                    var reloj = System.Diagnostics.Stopwatch.StartNew();
                    var r = await api.Get(ruta);
                    await r.Content.ReadAsByteArrayAsync();
                    reloj.Stop();
                    Assert.Equal(esperado, r.StatusCode);
                    mejor = Math.Min(mejor, reloj.ElapsedMilliseconds);
                }
                return mejor;
            }

            var sitemap = await Mejor("/sitemap.xml", HttpStatusCode.OK);
            var ficha = await Mejor("/vehiculo/qa-masivo-0500", HttpStatusCode.OK);
            salida.WriteLine($"sitemap.xml (1.000 fichas): {sitemap} ms; /vehiculo/qa-masivo-0500: {ficha} ms");
            Assert.True(sitemap < 500, $"sitemap.xml tardó {sitemap} ms");
            Assert.True(ficha < 500, $"/vehiculo/ tardó {ficha} ms");
        }
        finally
        {
            PlantillaPrueba.Borrar(plantilla);
        }
    }
}

/// <summary>QA P10 (B-5 de REVISION-SEGURIDAD-P10.md; CA-P10-68, 74): U+2028, U+2029 y ' se escapan en el JSON-LD.</summary>
public sealed class P10QaEscapeJsonLdTests
{
    // Los separadores se escriben con (char) para que el archivo fuente no los lleve crudos.
    private static readonly string L = ((char)0x2028).ToString();
    private static readonly string P = ((char)0x2029).ToString();
    private static readonly string Raros = $"Camión{L}línea{P}separada y 'comillas' del asesor";
    /// <summary>Secuencia de escape JSON de 6 caracteres (barra, u y 4 dígitos hex) del carácter dado.</summary>
    private static string EscapeJson(int codigo) => (char)92 + "u" + codigo.ToString("x4");

    [Fact(DisplayName = "QA B-5, CA-P10-68: U+2028, U+2029 y ' del asesor salen escapados en el JSON-LD de /vehiculo/{slug} (nunca crudos)")]
    public async Task Separadores_y_comilla_escapados()
    {
        var plantilla = PlantillaPrueba.Crear();
        try
        {
            using var api = await CatalogoApi.Crear(noVisibles: false, extra: s =>
            {
                var p = s.Pub("qa-separadores-2020", sinLinea: true, titulo: Raros);
                p.DescripcionComercial = Raros + " (descripción)";
            }, configurar: b => b.UseSetting(ConfiguracionPortal.ClaveRutaIndexHtml, plantilla));
            var r = await api.Get("/vehiculo/qa-separadores-2020");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var html = await r.Content.ReadAsStringAsync();
            var bloques = Regex.Matches(html, "<script type=\"application/ld[+]json\" data-camion=\"[^\"]+\">(.*?)</script>",
                RegexOptions.Singleline).Select(m => m.Groups[1].Value).ToArray();
            Assert.Equal(2, bloques.Length);
            foreach (var json in bloques)
            {
                Assert.DoesNotContain(L, json);
                Assert.DoesNotContain(P, json);
                Assert.DoesNotContain("'", json);
                Assert.Contains(EscapeJson(0x2028), json);
                Assert.Contains(EscapeJson(0x2029), json);
                Assert.Contains(EscapeJson(0x27), json);
            }
            // El JSON sigue siendo válido y da el texto original.
            var v = JsonDocument.Parse(bloques[0]).RootElement;
            Assert.Equal(Raros + " en Medellín", v.GetProperty("name").GetString());
            Assert.Equal(Raros + " (descripción)", v.GetProperty("description").GetString());
            // En todo el documento (title y og:* incluidos) no quedan separadores crudos ni la comilla simple sin codificar.
            Assert.DoesNotContain(L, html);
            Assert.DoesNotContain(P, html);
            Assert.DoesNotContain("'comillas'", html);
        }
        finally
        {
            PlantillaPrueba.Borrar(plantilla);
        }
    }
}
