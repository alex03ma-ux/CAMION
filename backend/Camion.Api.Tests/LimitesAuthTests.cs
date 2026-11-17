using System.Net;
using System.Net.Http.Json;
using Camion.Api.Data;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Camion.Api.Tests.Infraestructura.Ayudas;
using static Camion.Api.Tests.Infraestructura.AyudasP2;

namespace Camion.Api.Tests;

/// <summary>
/// P9 (HU-059, RN-SE-001, RN-SE-006, INF-2; CA-P9-38 a CA-P9-45, CA-P9-47): cupos por IP del registro (comprador y asesor
/// comparten cupo) y del inicio de sesión. Cada prueba usa su propia API con cupos bajos; la IP del cliente llega por
/// X-Forwarded-For desde un proxy de confianza (rangos de documentación). SQLite en memoria; nunca MariaDB.
/// </summary>
public sealed class LimitesAuthTests
{
    private const string MsgRegistro = "Hiciste muchos intentos de registro desde tu conexión. Intenta de nuevo más tarde.";
    private const string MsgLogin = "Hiciste muchos intentos de inicio de sesión. Espera unos minutos e intenta de nuevo.";
    private static readonly IPAddress Proxy = IPAddress.Parse("10.0.0.5");

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

    private sealed class Api : IDisposable
    {
        public CamionApiFactory Base { get; } = new();
        public WebApplicationFactory<Program> W { get; }
        public HttpClient C { get; }

        public Api(params (string Clave, string Valor)[] ajustes)
        {
            W = Base.WithWebHostBuilder(b =>
            {
                b.UseSetting("Proxy:KnownProxies", "10.0.0.5");
                foreach (var (clave, valor) in ajustes) b.UseSetting(clave, valor);
                b.ConfigureTestServices(s => s.AddSingleton<IStartupFilter>(new IpConexion(Proxy)));
            });
            C = W.CreateClient();
        }

        public Task<HttpResponseMessage> Post(string ruta, object cuerpo, string ip)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, ruta) { Content = JsonContent.Create(cuerpo) };
            req.Headers.TryAddWithoutValidation("X-Forwarded-For", ip);
            return C.SendAsync(req);
        }

        public Task<HttpResponseMessage> Comprador(string ip, string? email = null) =>
            Post("/api/auth/registro/comprador", Registro(email), ip);

        public Task<HttpResponseMessage> Asesor(string ip, string? email = null) =>
            Post("/api/auth/registro/asesor", RegistroAsesor(email), ip);

        public Task<HttpResponseMessage> Login(string ip, string email, string password) =>
            Post("/api/auth/login", new { email, password }, ip);

        public Task<T> Leer<T>(Func<CamionDbContext, Task<T>> consulta) => Db(W, consulta);

        public void Dispose()
        {
            C.Dispose();
            W.Dispose();
            Base.Dispose();
        }
    }

    private static async Task<T> Db<T>(WebApplicationFactory<Program> w, Func<CamionDbContext, Task<T>> consulta)
    {
        using var scope = w.Services.CreateScope();
        return await consulta(scope.ServiceProvider.GetRequiredService<CamionDbContext>());
    }

    private static async Task Es429(HttpResponseMessage r, string mensaje)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, r.StatusCode);
        Assert.Equal(mensaje, (await Json(r)).GetProperty("error").GetString());
        // CA-P9-42: Retry-After en segundos.
        Assert.True(r.Headers.TryGetValues("Retry-After", out var valores), "Falta Retry-After");
        Assert.True(int.TryParse(Assert.Single(valores), out var segundos) && segundos > 0);
    }

    [Fact(DisplayName = "CA-P9-38, 42, 47: comprador y asesor comparten el cupo de registro por IP; al superarlo -> 429 con mensaje y Retry-After, sin usuario ni consentimiento; el log no lleva IP ni correo")]
    public async Task Registro_cupo_compartido()
    {
        using var api = new Api(("LimitesAuth:RegistroLimiteVentana", "3"));
        const string ip = "203.0.113.40";
        Assert.Equal(HttpStatusCode.Created, (await api.Comprador(ip)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await api.Asesor(ip)).StatusCode);
        // También cuentan las peticiones rechazadas por validación.
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Post("/api/auth/registro/comprador", new { }, ip)).StatusCode);

        var usuarios = await api.Leer(db => db.Users.CountAsync());
        var consentimientos = await api.Leer(db => db.ConsentimientosDatos.CountAsync());
        var correo = CorreoUnico("limite");
        await Es429(await api.Comprador(ip, correo), MsgRegistro);
        await Es429(await api.Asesor(ip, correo), MsgRegistro);
        Assert.Equal(usuarios, await api.Leer(db => db.Users.CountAsync()));
        Assert.Equal(consentimientos, await api.Leer(db => db.ConsentimientosDatos.CountAsync()));

        // CA-P9-43: otra IPv4 tiene su propio cupo.
        Assert.Equal(HttpStatusCode.Created, (await api.Comprador("203.0.113.41")).StatusCode);

        // CA-P9-47: el rechazo se registra sin correo ni IP.
        var lineas = api.Base.Logs.Where(l => l.Contains("limite_auth", StringComparison.Ordinal)).ToArray();
        Assert.Contains(lineas, l => l.Contains("cupo=registro", StringComparison.Ordinal));
        foreach (var l in lineas)
        {
            Assert.DoesNotContain("203.0.113", l);
            Assert.DoesNotContain(correo, l);
        }
    }

    [Fact(DisplayName = "CA-P9-39: con cupo en la ventana corta, el límite diario por IP también responde 429")]
    public async Task Registro_limite_diario()
    {
        using var api = new Api(("LimitesAuth:RegistroLimiteVentana", "100"), ("LimitesAuth:RegistroLimiteDiario", "2"));
        const string ip = "203.0.113.50";
        Assert.Equal(HttpStatusCode.Created, (await api.Comprador(ip)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await api.Asesor(ip)).StatusCode);
        await Es429(await api.Comprador(ip), MsgRegistro);
    }

    [Fact(DisplayName = "CA-P9-40, 41, 42: más inicios de sesión que el cupo por IP -> 429; no cuenta como fallo de la cuenta, no la bloquea, no emite token ni cambia el último acceso")]
    public async Task Login_cupo_sin_fallo_de_cuenta()
    {
        using var api = new Api(("LimitesAuth:LoginLimiteVentana", "3"));
        const string ip = "203.0.113.60";
        var email = CorreoUnico("login");
        Assert.Equal(HttpStatusCode.Created, (await api.Comprador("203.0.113.61", email)).StatusCode);

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await api.Login(ip, email, "Incorrecta2026")).StatusCode);
        var antes = await api.Leer(db => db.Users.AsNoTracking().SingleAsync(u => u.Email == email));
        Assert.Equal(3, antes.AccessFailedCount);

        await Es429(await api.Login(ip, email, "Incorrecta2026"), MsgLogin);
        var correcta = await api.Login(ip, email, PasswordValida);
        await Es429(correcta, MsgLogin);
        Assert.DoesNotContain("accessToken", await correcta.Content.ReadAsStringAsync());

        var despues = await api.Leer(db => db.Users.AsNoTracking().SingleAsync(u => u.Email == email));
        Assert.Equal(3, despues.AccessFailedCount);
        Assert.Null(despues.LockoutEnd);
        Assert.Null(despues.UltimoAccesoEn);

        // Desde otra IP la cuenta sigue funcionando (no quedó bloqueada).
        Assert.Equal(HttpStatusCode.OK, (await api.Login("203.0.113.62", email, PasswordValida)).StatusCode);
    }

    [Fact(DisplayName = "CA-P9-43: dos IPv6 del mismo /64 comparten el cupo de inicio de sesión; otro /64 no")]
    public async Task Login_ipv6_por_64()
    {
        using var api = new Api(("LimitesAuth:LoginLimiteVentana", "2"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Login("2001:db8:9:1::a", "nadie@ejemplo.test", "Incorrecta2026")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Login("2001:db8:9:1::b", "nadie@ejemplo.test", "Incorrecta2026")).StatusCode);
        await Es429(await api.Login("2001:db8:9:1:ffff::1", "nadie@ejemplo.test", "Incorrecta2026"), MsgLogin);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Login("2001:db8:9:2::1", "nadie@ejemplo.test", "Incorrecta2026")).StatusCode);
    }

    [Fact(DisplayName = "CA-P9-44: los cupos de registro, inicio de sesión, contacto y restablecer son independientes; /me y /logout no tienen límite nuevo")]
    public async Task Cupos_independientes()
    {
        using var api = new Api(("LimitesAuth:RegistroLimiteVentana", "1"), ("LimitesAuth:LoginLimiteVentana", "1"));
        const string ip = "203.0.113.70";
        var email = CorreoUnico("indep");
        Assert.Equal(HttpStatusCode.Created, (await api.Comprador(ip, email)).StatusCode);
        await Es429(await api.Asesor(ip), MsgRegistro);

        // Registro agotado: el inicio de sesión sigue con su propio cupo.
        var login = await api.Login(ip, email, PasswordValida);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await Json(login)).GetProperty("accessToken").GetString()!;
        await Es429(await api.Login(ip, email, PasswordValida), MsgLogin);

        // Registro e inicio de sesión agotados: contacto y restablecer no se ven afectados.
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await api.Post("/api/contactos", new { }, ip)).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests,
            (await api.Post("/api/auth/restablecer-contrasena", new { codigo = "x", password = "x" }, ip)).StatusCode);
        for (var i = 0; i < 3; i++)
        {
            var me = ConToken(HttpMethod.Get, "/api/auth/me", token);
            me.Headers.TryAddWithoutValidation("X-Forwarded-For", ip);
            Assert.Equal(HttpStatusCode.OK, (await api.C.SendAsync(me)).StatusCode);
        }
        var logout = ConToken(HttpMethod.Post, "/api/auth/logout", token);
        logout.Headers.TryAddWithoutValidation("X-Forwarded-For", ip);
        Assert.Equal(HttpStatusCode.NoContent, (await api.C.SendAsync(logout)).StatusCode);
    }

    [Fact(DisplayName = "CA-P9-44: agotar el cupo de contacto no afecta al registro ni al inicio de sesión")]
    public async Task Contacto_agotado_no_afecta_auth()
    {
        using var api = new Api(("Contactos:LimiteVentanaCorta", "1"));
        const string ip = "203.0.113.80";
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await api.Post("/api/contactos", new { }, ip)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await api.Post("/api/contactos", new { }, ip)).StatusCode);
        var email = CorreoUnico("trascontacto");
        Assert.Equal(HttpStatusCode.Created, (await api.Comprador(ip, email)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Login(ip, email, PasswordValida)).StatusCode);
    }
}
