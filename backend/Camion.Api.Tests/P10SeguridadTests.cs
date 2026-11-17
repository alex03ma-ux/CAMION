using System.Net;
using Camion.Api.Endpoints;
using Camion.Api.Models;
using Camion.Api.Seo;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Camion.Api.Tests;

/// <summary>
/// P10: correcciones de la revisión de seguridad (docs/coordinacion/REVISION-SEGURIDAD-P10.md: M-1, B-2) y de la evidencia de
/// QA (docs/criterios/P10-evidencia.md: O-1, O-2).
/// </summary>
public sealed class P10SeguridadTests
{
    private const string Url = CamionApiFactory.UrlPortalPruebas;
    private static readonly IPAddress Proxy = IPAddress.Parse("10.0.0.5");

    /// <summary>Simula la IP de la conexión TCP (la del proxy), que TestServer deja vacía (como LimitesAuthTests).</summary>
    private sealed class IpConexion(IPAddress ip) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((contexto, siguiente) =>
            {
                contexto.Connection.RemoteIpAddress = ip;
                return siguiente(contexto);
            });
            next(app);
        };
    }

    private static Task<CatalogoApi> Api(string plantilla, params (string Clave, string Valor)[] ajustes) =>
        CatalogoApi.Crear(noVisibles: false, configurar: b =>
        {
            b.UseSetting(ConfiguracionPortal.ClaveRutaIndexHtml, plantilla);
            b.UseSetting(ConfiguracionPortal.ClavePermitirIndexacion, "true");
            b.UseSetting("Proxy:KnownProxies", "10.0.0.5");
            foreach (var (clave, valor) in ajustes) b.UseSetting(clave, valor);
            b.ConfigureTestServices(s => s.AddSingleton<IStartupFilter>(new IpConexion(Proxy)));
        });

    private static Task<HttpResponseMessage> Get(CatalogoApi api, string ruta, string ip)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, ruta);
        req.Headers.TryAddWithoutValidation("X-Forwarded-For", ip);
        return api.Cliente.SendAsync(req);
    }

    [Fact(DisplayName = "M-1: cupo por IP compartido entre /sitemap.xml y /vehiculo/*; 429 texto con Retry-After; otra IP y robots.txt siguen")]
    public async Task Cupo_por_ip()
    {
        var plantilla = PlantillaPrueba.Crear();
        try
        {
            using var api = await Api(plantilla, ("LimitesSeo:LimiteVentana", "3"), ("LimitesSeo:VentanaMinutos", "5"));
            const string ip = "203.0.113.7";
            Assert.Equal(HttpStatusCode.OK, (await Get(api, "/sitemap.xml", ip)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Get(api, "/vehiculo/chevrolet-npr-2020", ip)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Get(api, "/vehiculo/no-existe", ip)).StatusCode);
            foreach (var ruta in new[] { "/sitemap.xml", "/vehiculo/chevrolet-npr-2020" })
            {
                var r = await Get(api, ruta, ip);
                Assert.Equal(HttpStatusCode.TooManyRequests, r.StatusCode);
                Assert.Equal(LimitePeticionesSeo.Mensaje, await r.Content.ReadAsStringAsync());
                Assert.Equal("text/plain; charset=utf-8", r.Content.Headers.ContentType!.ToString());
                var espera = int.Parse(Assert.Single(r.Headers.GetValues("Retry-After")));
                Assert.InRange(espera, 1, 300);
            }
            // robots.txt (barato, sin base) no tiene cupo; el catálogo JSON tampoco cambia.
            Assert.Equal(HttpStatusCode.OK, (await Get(api, "/robots.txt", ip)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Get(api, "/api/catalogo/inicio", ip)).StatusCode);
            // Otra IP tiene su propio cupo.
            Assert.Equal(HttpStatusCode.OK, (await Get(api, "/sitemap.xml", "198.51.100.9")).StatusCode);
        }
        finally
        {
            PlantillaPrueba.Borrar(plantilla);
        }
    }

    [Fact(DisplayName = "M-1: IPv6 del mismo /64 comparte cupo; otro /64 no")]
    public async Task Cupo_ipv6_por_64()
    {
        var plantilla = PlantillaPrueba.Crear();
        try
        {
            using var api = await Api(plantilla, ("LimitesSeo:LimiteVentana", "2"));
            Assert.Equal(HttpStatusCode.OK, (await Get(api, "/sitemap.xml", "2001:db8:1:2::10")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Get(api, "/sitemap.xml", "2001:db8:1:2:aaaa::1")).StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, (await Get(api, "/sitemap.xml", "2001:db8:1:2:ffff::2")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Get(api, "/sitemap.xml", "2001:db8:1:3::10")).StatusCode);
        }
        finally
        {
            PlantillaPrueba.Borrar(plantilla);
        }
    }

    [Fact(DisplayName = "M-1: el sitemap se reutiliza durante LimitesSeo:SitemapCacheMinutos y caduca solo")]
    public async Task Sitemap_en_cache()
    {
        var plantilla = PlantillaPrueba.Crear();
        try
        {
            using var api = await Api(plantilla, ("LimitesSeo:SitemapCacheMinutos", "10"));
            const string ip = "203.0.113.8";
            const string loc = Url + "/vehiculo/renault-logan-2021";
            Assert.Contains(loc, await (await Get(api, "/sitemap.xml", ip)).Content.ReadAsStringAsync());
            await api.Db(async db =>
                (await db.Publicaciones.SingleAsync(p => p.SlugPublico == "renault-logan-2021")).EstadoComercial =
                Publicacion.Vendido);

            // Dentro de los 10 minutos: el mismo XML, sin volver a consultar.
            api.Reloj.Ahora = api.Reloj.Ahora.AddMinutes(9);
            Assert.Contains(loc, await (await Get(api, "/sitemap.xml", ip)).Content.ReadAsStringAsync());
            // Pasados los 10 minutos, se regenera y el vendido ya no está (CA-P10-60).
            api.Reloj.Ahora = api.Reloj.Ahora.AddMinutes(2);
            Assert.DoesNotContain(loc, await (await Get(api, "/sitemap.xml", ip)).Content.ReadAsStringAsync());
        }
        finally
        {
            PlantillaPrueba.Borrar(plantilla);
        }
    }

    [Fact(DisplayName = "M-1: valores por defecto generosos (120 por minuto) y caché de 10 minutos")]
    public void Valores_por_defecto()
    {
        var o = new OpcionesLimitesSeo();
        Assert.Equal(120, o.LimiteVentana);
        Assert.Equal(1, o.VentanaMinutos);
        Assert.Equal(10, o.SitemapCacheMinutos);
    }

    [Fact(DisplayName = "B-2: Portal:RutaIndexHtml solo acepta .html y como mucho 512 KB; si no, la API no arranca")]
    public void Plantilla_html_y_tamano()
    {
        var raiz = Path.GetTempPath();
        foreach (var mala in new[] { "appsettings.json", "index.htm", "index.html.bak", "index" })
            Assert.Contains(".html", Assert.Throws<InvalidOperationException>(() =>
                ConfiguracionPortal.RutaIndexHtml(mala, esDesarrollo: true, raiz)).Message);
        Assert.NotNull(ConfiguracionPortal.RutaIndexHtml("dist/INDEX.HTML", esDesarrollo: true, raiz));

        var grande = Path.Combine(Path.GetTempPath(), $"camion_index_grande_{Guid.NewGuid():N}.html");
        try
        {
            File.WriteAllText(grande, PlantillaPrueba.Completa + new string(' ', (int)ConfiguracionPortal.MaxBytesIndexHtml));
            var ex = Assert.Throws<InvalidOperationException>(() => ConfiguracionPortal.RutaIndexHtml(grande, false, raiz));
            Assert.Contains("512 KB", ex.Message);
            Assert.DoesNotContain(grande, ex.Message);
        }
        finally
        {
            PlantillaPrueba.Borrar(grande);
        }

        // Fuera de Development, con un archivo que no es .html, la API no arranca.
        var json = Path.Combine(Path.GetTempPath(), $"camion_no_html_{Guid.NewGuid():N}.json");
        File.WriteAllText(json, "{}");
        try
        {
            using var f = new CamionApiFactory();
            using var w = f.WithWebHostBuilder(b => b.UseSetting(ConfiguracionPortal.ClaveRutaIndexHtml, json));
            var ex = Assert.ThrowsAny<Exception>(() => w.CreateClient());
            Assert.Contains(ConfiguracionPortal.ClaveRutaIndexHtml, (ex.InnerException ?? ex).Message + ex.Message);
        }
        finally
        {
            PlantillaPrueba.Borrar(json);
        }
    }

    [Fact(DisplayName = "B-2: si la plantilla crece por encima de 512 KB con la API en marcha, 503 y error en el log (una vez)")]
    public async Task Plantilla_crece()
    {
        var plantilla = PlantillaPrueba.Crear();
        try
        {
            using var api = await Api(plantilla);
            Assert.Equal(HttpStatusCode.OK, (await Get(api, "/vehiculo/chevrolet-npr-2020", "203.0.113.20")).StatusCode);
            File.WriteAllText(plantilla, PlantillaPrueba.Completa + new string(' ', (int)ConfiguracionPortal.MaxBytesIndexHtml));
            File.SetLastWriteTimeUtc(plantilla, DateTime.UtcNow.AddMinutes(5));
            for (var i = 0; i < 2; i++)
            {
                var r = await Get(api, "/vehiculo/chevrolet-npr-2020", "203.0.113.20");
                Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
                Assert.Equal(SeoEndpoints.MensajeNoDisponible, await r.Content.ReadAsStringAsync());
            }
            var error = Assert.Single(api.Base.Logs, l => l.Contains("|Error|") && l.Contains("512 KB"));
            Assert.DoesNotContain(plantilla, error);
        }
        finally
        {
            PlantillaPrueba.Borrar(plantilla);
        }
    }

    [Fact(DisplayName = "O-1: /vehiculo/<slug>/ redirige 301 a /vehiculo/<slug> (con la consulta); dos barras o más segmentos, 404")]
    public async Task Barra_final_redirige()
    {
        var plantilla = PlantillaPrueba.Crear();
        try
        {
            using var api = await Api(plantilla);
            using var c = api.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var r = await c.GetAsync("/vehiculo/chevrolet-npr-2020/");
            Assert.Equal(HttpStatusCode.MovedPermanently, r.StatusCode);
            Assert.Equal("/vehiculo/chevrolet-npr-2020", r.Headers.Location!.OriginalString);

            var conConsulta = await c.GetAsync("/vehiculo/chevrolet-npr-2020/?utm_source=whatsapp");
            Assert.Equal(HttpStatusCode.MovedPermanently, conConsulta.StatusCode);
            Assert.Equal("/vehiculo/chevrolet-npr-2020?utm_source=whatsapp", conConsulta.Headers.Location!.OriginalString);

            // El slug va codificado igual en la redirección.
            var especial = await c.GetAsync("/vehiculo/" + Uri.EscapeDataString("camion-ñ&x-2020") + "/");
            Assert.Equal(HttpStatusCode.MovedPermanently, especial.StatusCode);
            Assert.Equal("/vehiculo/" + Uri.EscapeDataString("camion-ñ&x-2020"), especial.Headers.Location!.OriginalString);

            // También para un slug no visible: el destino da el 404 de siempre.
            Assert.Equal(HttpStatusCode.MovedPermanently, (await c.GetAsync("/vehiculo/no-existe/")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/vehiculo/no-existe")).StatusCode);

            foreach (var ruta in new[] { "/vehiculo/chevrolet-npr-2020//", "/vehiculo/a/b/", "/vehiculo/" })
            {
                var x = await c.GetAsync(ruta);
                Assert.Equal(HttpStatusCode.NotFound, x.StatusCode);
                Assert.Equal("noindex", Assert.Single(x.Headers.GetValues("X-Robots-Tag")));
            }

            // Siguiendo la redirección se llega a la ficha.
            Assert.Equal(HttpStatusCode.OK, (await api.Cliente.GetAsync("/vehiculo/chevrolet-npr-2020/")).StatusCode);
        }
        finally
        {
            PlantillaPrueba.Borrar(plantilla);
        }
    }

    [Fact(DisplayName = "O-2: /Vehiculo/<slug> y /VEHICULO/<slug> dan el 404 genérico con noindex, como el SPA")]
    public async Task Mayusculas_404()
    {
        var plantilla = PlantillaPrueba.Crear();
        try
        {
            using var api = await Api(plantilla);
            using var c = api.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var generico = await (await c.GetAsync("/vehiculo/no-existe")).Content.ReadAsStringAsync();
            foreach (var ruta in new[] { "/Vehiculo/chevrolet-npr-2020", "/VEHICULO/chevrolet-npr-2020", "/Vehiculo/chevrolet-npr-2020/" })
            {
                var r = await c.GetAsync(ruta);
                Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
                Assert.Equal(generico, await r.Content.ReadAsStringAsync());
                Assert.Equal("noindex", Assert.Single(r.Headers.GetValues("X-Robots-Tag")));
            }
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/vehiculo/chevrolet-npr-2020")).StatusCode);
        }
        finally
        {
            PlantillaPrueba.Borrar(plantilla);
        }
    }
}
