using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Camion.Api.Auth;
using Camion.Api.Data;
using Camion.Api.Seguridad;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Camion.Api.Tests;

/// <summary>
/// INF-2: seguridad del servidor para publicar (M-1 y B-4 de P5, M-1 de P8). SQLite en memoria; nunca MariaDB.
/// Las IP son de los rangos de documentación (203.0.113.0/24, 2001:db8::/32) y de redes privadas.
/// </summary>
public sealed class InfPublicacionTests
{
    private static readonly IPAddress Proxy = IPAddress.Parse("10.0.0.5");
    private const string RutaRestablecer = "/api/auth/restablecer-contrasena";

    /// <summary>Simula la IP de la conexión TCP (la del proxy), que TestServer deja vacía.</summary>
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

    private static (CamionApiFactory Base, HttpClient C) Api(string? entorno = null, params (string Clave, string Valor)[] ajustes)
    {
        var f = new CamionApiFactory();
        var w = f.WithWebHostBuilder(b =>
        {
            if (entorno is not null) b.UseEnvironment(entorno);
            // Cupo bajo de restablecer la contraseña (2 por ventana) para ver el límite por IP con pocas peticiones.
            b.UseSetting("Recuperacion:LimiteVentana", "2");
            foreach (var (clave, valor) in ajustes) b.UseSetting(clave, valor);
            b.ConfigureTestServices(s => s.AddSingleton<IStartupFilter>(new IpConexion(Proxy)));
        });
        var c = w.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        // Un host que no sea localhost: HSTS no se envía a localhost.
        c.BaseAddress = new Uri("http://portal.ejemplo.test");
        return (f, c);
    }

    private static async Task<HttpStatusCode> Restablecer(HttpClient c, string? reenviadoPor)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, RutaRestablecer)
        {
            Content = JsonContent.Create(new { codigo = "x", password = "x" })
        };
        if (reenviadoPor is not null) req.Headers.TryAddWithoutValidation("X-Forwarded-For", reenviadoPor);
        return (await c.SendAsync(req)).StatusCode;
    }

    // ------------------------------------------------------------------ M-1 de P5: IP real y /64

    [Fact(DisplayName = "INF-2 (M-1 de P5): detrás de un proxy de confianza cada cliente tiene su propio cupo por IP")]
    public async Task Proxy_de_confianza_cupo_por_cliente()
    {
        var (f, c) = Api(null, ("Proxy:KnownNetworks", "10.0.0.0/8"));
        using (f)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, await Restablecer(c, "203.0.113.10"));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, await Restablecer(c, "203.0.113.10"));
            Assert.Equal(HttpStatusCode.TooManyRequests, await Restablecer(c, "203.0.113.10"));
            // Otro visitante detrás del mismo proxy no queda bloqueado.
            Assert.NotEqual(HttpStatusCode.TooManyRequests, await Restablecer(c, "203.0.113.11"));
        }
    }

    [Fact(DisplayName = "INF-2 (M-1 de P5): IPv6 se agrupa por /64 (rotar direcciones del mismo /64 no salta el límite)")]
    public async Task Ipv6_por_prefijo_64()
    {
        var (f, c) = Api(null, ("Proxy:KnownProxies", "10.0.0.5"));
        using (f)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, await Restablecer(c, "2001:db8:1:2::a"));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, await Restablecer(c, "2001:db8:1:2::b"));
            Assert.Equal(HttpStatusCode.TooManyRequests, await Restablecer(c, "2001:db8:1:2:ffff::1"));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, await Restablecer(c, "2001:db8:1:3::1"));
        }
    }

    [Theory(DisplayName = "INF-2 (M-1 de P5): sin proxy configurado, o desde un proxy que no es de confianza, X-Forwarded-For se ignora")]
    [InlineData(null)]
    [InlineData("10.0.0.9")]
    public async Task Sin_proxy_de_confianza_se_ignora_el_encabezado(string? proxiesConfigurados)
    {
        var (f, c) = proxiesConfigurados is null ? Api() : Api(null, ("Proxy:KnownProxies", proxiesConfigurados));
        using (f)
        {
            // Tres "clientes" distintos según el encabezado, pero todos llegan desde la misma conexión: comparten cupo.
            Assert.NotEqual(HttpStatusCode.TooManyRequests, await Restablecer(c, "203.0.113.20"));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, await Restablecer(c, "203.0.113.21"));
            Assert.Equal(HttpStatusCode.TooManyRequests, await Restablecer(c, "203.0.113.22"));
        }
    }

    [Theory(DisplayName = "INF-2 (M-1 de P5): clave del límite por IP (IPv4, IPv4 mapeada e IPv6 /64)")]
    [InlineData(null, "sin-ip")]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:1:2:3:4:5:6", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2::", "2001:db8:1:2::/64")]
    [InlineData("::1", "::/64")]
    public void Clave_ip(string? ip, string esperada) =>
        Assert.Equal(esperada, ConfiguracionServidor.ClaveIp(ip is null ? null : IPAddress.Parse(ip)));

    [Fact(DisplayName = "INF-2 (M-1 de P5): proxies y redes desde lista o texto; sin configurar no hay ForwardedHeaders; mal escritos, no arranca")]
    public void Opciones_proxy()
    {
        Assert.Null(ConfiguracionServidor.OpcionesProxy(Config()));

        var o = ConfiguracionServidor.OpcionesProxy(Config(("Proxy:KnownProxies", "10.0.0.5; 10.0.0.6"),
            ("Proxy:KnownNetworks:0", "172.16.0.0/12"), ("Proxy:KnownNetworks:1", "fd00::/8")))!;
        Assert.Equal(new[] { "10.0.0.5", "10.0.0.6" }, o.KnownProxies.Select(x => x.ToString()).ToArray());
        Assert.Equal(2, o.KnownNetworks.Count);
        // Las direcciones de bucle local que ASP.NET trae por defecto no quedan como de confianza.
        Assert.DoesNotContain(IPAddress.Loopback, o.KnownProxies);
        Assert.Equal(1, o.ForwardLimit);
        Assert.Equal(2, ConfiguracionServidor.OpcionesProxy(Config(("Proxy:KnownProxies", "10.0.0.5"), ("Proxy:ForwardLimit", "2")))!.ForwardLimit);

        foreach (var malo in new[] { ("Proxy:KnownProxies", "10.0.0.300"), ("Proxy:KnownNetworks", "10.0.0.0"),
                     ("Proxy:KnownNetworks", "10.0.0.0/33"), ("Proxy:KnownNetworks", "fd00::/129") })
            Assert.Throws<InvalidOperationException>(() => ConfiguracionServidor.OpcionesProxy(Config(malo)));
        Assert.Throws<InvalidOperationException>(() =>
            ConfiguracionServidor.OpcionesProxy(Config(("Proxy:KnownProxies", "10.0.0.5"), ("Proxy:ForwardLimit", "0"))));
    }

    // ------------------------------------------------------------------ producción: https, HSTS, encabezados

    [Fact(DisplayName = "INF-2: encabezados de seguridad en todas las respuestas (también 401, 404 y errores de formato)")]
    public async Task Encabezados_de_seguridad()
    {
        var (f, c) = Api();
        using (f)
        {
            var respuestas = new[]
            {
                await c.GetAsync("/health"),
                await c.GetAsync("/api/auth/me"),
                await c.GetAsync("/media/vehiculos/1/no-existe.jpg"),
                await c.PostAsync("/api/auth/login", new StringContent("{no es json", System.Text.Encoding.UTF8, "application/json"))
            };
            foreach (var r in respuestas)
            {
                foreach (var (nombre, valor) in ConfiguracionServidor.Encabezados)
                    Assert.Equal(valor, Assert.Single(r.Headers.GetValues(nombre)));
                Assert.False(r.Headers.Contains("Server"));
            }
            Assert.Equal("nosniff", Assert.Single(respuestas[0].Headers.GetValues("X-Content-Type-Options")));
            Assert.Equal("DENY", Assert.Single(respuestas[0].Headers.GetValues("X-Frame-Options")));
        }
    }

    [Fact(DisplayName = "INF-2: en Production http -> https (307) y HSTS; con Https:RedirigirHttp=false no redirige")]
    public async Task Produccion_https_y_hsts()
    {
        var (f, c) = Api("Production", ("https_port", "443"));
        using (f)
        {
            var r = await c.GetAsync("/health");
            Assert.Equal(HttpStatusCode.TemporaryRedirect, r.StatusCode);
            Assert.Equal("https://portal.ejemplo.test/health", r.Headers.Location!.ToString());
        }

        var (f2, c2) = Api("Production", ("https_port", "443"), ("Https:RedirigirHttp", "false"));
        using (f2)
        {
            var r = await c2.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.False(r.Headers.Contains("Strict-Transport-Security")); // HSTS solo se envía por https
        }
    }

    [Fact(DisplayName = "INF-2: el proxy que termina TLS manda X-Forwarded-Proto: https -> sin redirección y con HSTS (ForwardedHeaders va antes)")]
    public async Task Produccion_detras_de_proxy_tls()
    {
        var (f, c) = Api("Production", ("https_port", "443"), ("Proxy:KnownProxies", "10.0.0.5"));
        using (f)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "/health");
            req.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.30");
            req.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
            var r = await c.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal("max-age=2592000", Assert.Single(r.Headers.GetValues("Strict-Transport-Security")));

            // El mismo encabezado desde una conexión que no es el proxy no vale: se redirige.
            var (f2, c2) = Api("Production", ("https_port", "443"));
            using (f2)
            {
                using var req2 = new HttpRequestMessage(HttpMethod.Get, "/health");
                req2.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
                Assert.Equal(HttpStatusCode.TemporaryRedirect, (await c2.SendAsync(req2)).StatusCode);
            }
        }
    }

    [Fact(DisplayName = "INF-2: en Production un error no controlado responde 500 genérico, sin detalles ni pila")]
    public async Task Produccion_error_sin_detalles()
    {
        var f = new CamionApiFactory();
        using (f)
        {
            var w = f.WithWebHostBuilder(b =>
            {
                b.UseEnvironment("Production");
                b.ConfigureTestServices(s => s.AddSingleton<Camion.Api.Media.IAlmacenFotos>(new AlmacenQueFalla()));
            });
            var r = await w.CreateClient().GetAsync("/media/vehiculos/1/foto.jpg");
            Assert.Equal(HttpStatusCode.InternalServerError, r.StatusCode);
            var texto = await r.Content.ReadAsStringAsync();
            Assert.DoesNotContain("FallaFicticia", texto);
            Assert.DoesNotContain("   at ", texto);
            Assert.Contains("\"error\"", texto);
        }
    }

    /// <summary>Almacén que falla dentro de un endpoint, para provocar un error no controlado.</summary>
    private sealed class AlmacenQueFalla : Camion.Api.Media.IAlmacenFotos
    {
        private static Exception Falla() => new InvalidOperationException("FallaFicticia con datos internos");
        public Task GuardarAsync(string clave, Stream contenido, CancellationToken ct) => throw Falla();
        public void Eliminar(string clave) => throw Falla();
        public string? RutaSegura(string clave) => throw Falla();
        public IReadOnlyList<string> Listar(string carpeta) => throw Falla();
    }

    // ------------------------------------------------------------------ claves obligatorias

    [Fact(DisplayName = "INF-2: fuera de Development faltan Media:Ruta, DataProtection:RutaClaves o Recuperacion:UrlPortal -> no arranca (solo nombres)")]
    public void Claves_obligatorias()
    {
        ConfiguracionServidor.ValidarObligatorias(Config(), esDesarrollo: true);
        var ex = Assert.Throws<InvalidOperationException>(() => ConfiguracionServidor.ValidarObligatorias(
            Config(("Media:Ruta", "  "), ("Recuperacion:UrlPortal", "https://portal.ejemplo.test")), esDesarrollo: false));
        Assert.Contains("Media:Ruta", ex.Message);
        Assert.Contains("DataProtection:RutaClaves", ex.Message);
        Assert.DoesNotContain("Recuperacion:UrlPortal", ex.Message);
        Assert.DoesNotContain("portal.ejemplo.test", ex.Message);
        ConfiguracionServidor.ValidarObligatorias(Config(("Media:Ruta", "/srv/camion/media"),
            ("DataProtection:RutaClaves", "/srv/camion/claves"), ("Recuperacion:UrlPortal", "https://portal.ejemplo.test")), false);
    }

    [Fact(DisplayName = "INF-2: en Production la API no arranca sin Media:Ruta")]
    public void Produccion_sin_media_no_arranca()
    {
        using var f = new CamionApiFactory();
        using var prod = f.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("Media:Ruta", "");
        });
        var ex = Assert.ThrowsAny<Exception>(() => prod.CreateClient());
        Assert.Contains("Media:Ruta", (ex.InnerException ?? ex).Message + ex.Message);
    }

    // ------------------------------------------------------------------ CORS

    [Fact(DisplayName = "INF-2: CORS solo para Portal:Origen; sin configurarlo ninguna respuesta lleva Access-Control-Allow-Origin")]
    public async Task Cors_restringido()
    {
        const string origen = "https://portal.ejemplo.test";
        var (f, c) = Api(null, ("Portal:Origen", origen));
        using (f)
        {
            var bien = await c.SendAsync(Preflight(origen));
            Assert.Equal(origen, Assert.Single(bien.Headers.GetValues("Access-Control-Allow-Origin")));
            var otro = await c.SendAsync(Preflight("https://otro.ejemplo.test"));
            Assert.False(otro.Headers.Contains("Access-Control-Allow-Origin"));
        }
        var (f2, c2) = Api();
        using (f2)
        {
            var r = await c2.SendAsync(Preflight(origen));
            Assert.False(r.Headers.Contains("Access-Control-Allow-Origin"));
            using var get = new HttpRequestMessage(HttpMethod.Get, "/health");
            get.Headers.Add("Origin", origen);
            Assert.False((await c2.SendAsync(get)).Headers.Contains("Access-Control-Allow-Origin"));
        }
    }

    private static HttpRequestMessage Preflight(string origen)
    {
        var req = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        req.Headers.Add("Origin", origen);
        req.Headers.Add("Access-Control-Request-Method", "POST");
        req.Headers.Add("Access-Control-Request-Headers", "content-type");
        return req;
    }

    [Theory(DisplayName = "INF-2: Portal:Origen es un origen sin ruta y https fuera de Development")]
    [InlineData("", false, null)]
    [InlineData("https://portal.ejemplo.test", false, "https://portal.ejemplo.test")]
    [InlineData("https://portal.ejemplo.test/", false, "https://portal.ejemplo.test")]
    [InlineData("http://localhost:5173", true, "http://localhost:5173")]
    [InlineData("http://portal.ejemplo.test", false, "x")]
    [InlineData("https://portal.ejemplo.test/ruta", false, "x")]
    [InlineData("portal.ejemplo.test", false, "x")]
    public void Origen_portal(string valor, bool desarrollo, string? esperado)
    {
        var c = Config(("Portal:Origen", valor));
        if (esperado == "x")
            Assert.Contains("Portal:Origen", Assert.Throws<InvalidOperationException>(() => ConfiguracionServidor.OrigenPortal(c, desarrollo)).Message);
        else Assert.Equal(esperado, ConfiguracionServidor.OrigenPortal(c, desarrollo));
    }

    // ------------------------------------------------------------------ DataProtection con certificado (M-1 de P8)

    [Fact(DisplayName = "INF-2 (M-1 de P8): con DataProtection:Certificado:Ruta las claves se guardan cifradas con el certificado y se pueden leer")]
    public void Claves_cifradas_con_certificado()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), $"camion_dp_pruebas_{Guid.NewGuid():N}");
        Directory.CreateDirectory(carpeta);
        try
        {
            // Clave ficticia generada en la prueba; nunca un secreto real.
            var clave = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
            var pfx = Path.Combine(carpeta, "dp.pfx");
            using (var rsa = RSA.Create(2048))
            {
                var solicitud = new CertificateRequest("CN=Camion DataProtection Pruebas", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using var cert = solicitud.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
                File.WriteAllBytes(pfx, cert.Export(X509ContentType.Pfx, clave));
                File.WriteAllBytes(Path.Combine(carpeta, "publico.cer"), cert.Export(X509ContentType.Cert));
            }
            var claves = Path.Combine(carpeta, "claves");
            string protegido;
            using (var sp = Proveedor(claves, pfx, clave))
            {
                protegido = sp.GetRequiredService<IDataProtectionProvider>().CreateProtector("prueba").Protect("hola");
            }
            var xml = File.ReadAllText(Assert.Single(Directory.GetFiles(claves, "*.xml")));
            Assert.Contains("encryptedSecret", xml);
            Assert.DoesNotContain("<masterKey", xml.Replace("encryptedSecret", ""), StringComparison.Ordinal);
            // Otro arranque (otro proveedor) lee las claves guardadas con el mismo certificado.
            using (var sp = Proveedor(claves, pfx, clave))
                Assert.Equal("hola", sp.GetRequiredService<IDataProtectionProvider>().CreateProtector("prueba").Unprotect(protegido));

            // Errores claros y sin la clave: archivo inexistente, clave incorrecta y certificado sin clave privada.
            Assert.Contains(ConfiguracionServidor.ClaveCertificadoRuta, Assert.Throws<InvalidOperationException>(() =>
                ConfiguracionServidor.CargarCertificado(Path.Combine(carpeta, "no.pfx"), clave)).Message);
            var mala = Assert.Throws<InvalidOperationException>(() => ConfiguracionServidor.CargarCertificado(pfx, "incorrecta-ficticia"));
            Assert.DoesNotContain("incorrecta-ficticia", mala.Message);
            Assert.Null(mala.InnerException);
            Assert.Contains("clave privada", Assert.Throws<InvalidOperationException>(() =>
                ConfiguracionServidor.CargarCertificado(Path.Combine(carpeta, "publico.cer"), null)).Message);
        }
        finally
        {
            try { Directory.Delete(carpeta, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static ServiceProvider Proveedor(string claves, string pfx, string clave)
    {
        var servicios = new ServiceCollection();
        var dp = servicios.AddDataProtection().SetApplicationName(ConfiguracionRecuperacion.NombreAplicacion)
            .PersistKeysToFileSystem(new DirectoryInfo(claves));
        Assert.Null(ConfiguracionServidor.ProtegerClaves(dp, Config(
            (ConfiguracionServidor.ClaveCertificadoRuta, pfx), (ConfiguracionServidor.ClaveCertificadoClave, clave))));
        return servicios.BuildServiceProvider();
    }

    // ------------------------------------------------------------------ B-4 de P5: MySqlConnector 2.6 con Pomelo 8

    [Fact(DisplayName = "INF-2 (B-4 de P5): Pomelo 8 con MySqlConnector 2.6 construye el modelo y genera el SQL de MariaDB sin conectarse")]
    public void Pomelo_con_mysqlconnector_actual()
    {
        var opciones = new DbContextOptionsBuilder<CamionDbContext>()
            .UseMySql("Server=127.0.0.1;Port=1;Database=prueba_inexistente;User=prueba_ficticia",
                new MariaDbServerVersion(new Version(10, 4, 32)))
            .Options;
        using var db = new CamionDbContext(opciones);
        var sql = db.Database.GenerateCreateScript();
        Assert.Contains("CREATE TABLE `leads`", sql);
        Assert.Contains("CREATE TABLE `AspNetUsers`", sql);
        var version = typeof(MySqlConnector.MySqlConnection).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion.Split('+', '-')[0];
        Assert.True(Version.Parse(version) >= new Version(2, 6, 1), $"MySqlConnector {version} tiene el aviso GHSA-473q-m89c-ghf8");
    }

    private static IConfiguration Config(params (string Clave, string Valor)[] valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores.Select(v => new KeyValuePair<string, string?>(v.Clave, v.Valor))).Build();
}
