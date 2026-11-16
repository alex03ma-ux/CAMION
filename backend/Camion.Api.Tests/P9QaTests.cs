using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Camion.Api.Auth;
using Camion.Api.Data;
using Camion.Api.Models;
using Camion.Api.Parametros;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static Camion.Api.Tests.Infraestructura.Ayudas;
using static Camion.Api.Tests.Infraestructura.AyudasP2;
using static Camion.Api.Tests.Infraestructura.AyudasP4p;
using static Camion.Api.Tests.Infraestructura.AyudasP7;

namespace Camion.Api.Tests;

/// <summary>
/// QA de P9 (casos negativos y bordes añadidos por QA; CA-P9-08, 29, 30, 32, 38, 40, 41, 43, 44): proxy que no es de
/// confianza, IPv6 /64 en el registro, 429 en el umbral del bloqueo de la cuenta, valores por defecto de LimitesAuth,
/// sesión no válida en /api/admin/parametros/empresa y bordes del NIT y de las longitudes. SQLite en memoria; nunca
/// MariaDB. Datos ficticios (rangos de documentación 203.0.113.0/24 y 2001:db8::/32).
/// </summary>
public sealed class P9QaTests
{
    private const string MsgRegistro = LimitePeticionesAuth.MensajeRegistro;
    private const string MsgLogin = LimitePeticionesAuth.MensajeLogin;
    private const string RutaEmpresa = "/api/admin/parametros/empresa";

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

    /// <summary>API propia; la conexión TCP llega desde 10.0.0.5. <paramref name="proxyConfianza"/> null = sin proxies.</summary>
    private sealed class Api : IDisposable
    {
        public CamionApiFactory Base { get; } = new();
        public WebApplicationFactory<Program> W { get; }
        public HttpClient C { get; }

        public Api(string? proxyConfianza, params (string Clave, string Valor)[] ajustes)
        {
            W = Base.WithWebHostBuilder(b =>
            {
                if (proxyConfianza is not null) b.UseSetting("Proxy:KnownProxies", proxyConfianza);
                foreach (var (clave, valor) in ajustes) b.UseSetting(clave, valor);
                b.ConfigureTestServices(s => s.AddSingleton<IStartupFilter>(new IpConexion(IPAddress.Parse("10.0.0.5"))));
            });
            C = W.CreateClient();
        }

        public Task<HttpResponseMessage> Post(string ruta, object cuerpo, string ip, string? token = null)
        {
            var req = ConToken(HttpMethod.Post, ruta, token);
            req.Content = JsonContent.Create(cuerpo);
            req.Headers.TryAddWithoutValidation("X-Forwarded-For", ip);
            return C.SendAsync(req);
        }

        public Task<HttpResponseMessage> Get(string ruta, string ip, string? token)
        {
            var req = ConToken(HttpMethod.Get, ruta, token);
            req.Headers.TryAddWithoutValidation("X-Forwarded-For", ip);
            return C.SendAsync(req);
        }

        public Task<HttpResponseMessage> Comprador(string ip, string? email = null) =>
            Post("/api/auth/registro/comprador", Registro(email), ip);

        public Task<HttpResponseMessage> Asesor(string ip, string? email = null) =>
            Post("/api/auth/registro/asesor", RegistroAsesor(email), ip);

        public Task<HttpResponseMessage> Login(string ip, string email, string password) =>
            Post("/api/auth/login", new { email, password }, ip);

        public async Task<T> Leer<T>(Func<CamionDbContext, Task<T>> consulta)
        {
            using var scope = W.Services.CreateScope();
            return await consulta(scope.ServiceProvider.GetRequiredService<CamionDbContext>());
        }

        public void Dispose()
        {
            C.Dispose();
            W.Dispose();
            Base.Dispose();
        }
    }

    private static string RutaFuente([System.Runtime.CompilerServices.CallerFilePath] string ruta = "") => ruta;

    private static async Task Es429(HttpResponseMessage r, string mensaje)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, r.StatusCode);
        Assert.Equal(mensaje, (await Json(r)).GetProperty("error").GetString());
        Assert.True(r.Headers.TryGetValues("Retry-After", out _), "Falta Retry-After");
    }

    // ------------------------------------------------------------ CA-P9-43: X-Forwarded-For solo desde un proxy de confianza

    [Theory(DisplayName = "QA CA-P9-43: sin proxy configurado o desde un proxy que no es de confianza, cambiar X-Forwarded-For no salta el cupo de inicio de sesión")]
    [InlineData(null)]
    [InlineData("10.0.0.9")]
    public async Task Login_xff_sin_confianza_no_salta_el_cupo(string? proxy)
    {
        using var api = new Api(proxy, ("LimitesAuth:LoginLimiteVentana", "2"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Login("203.0.113.101", "nadie@ejemplo.test", "Incorrecta2026")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Login("203.0.113.102", "nadie@ejemplo.test", "Incorrecta2026")).StatusCode);
        await Es429(await api.Login("203.0.113.103", "nadie@ejemplo.test", "Incorrecta2026"), MsgLogin);
    }

    [Theory(DisplayName = "QA CA-P9-43: sin proxy de confianza, cambiar X-Forwarded-For no salta el cupo de registro")]
    [InlineData(null)]
    [InlineData("10.0.0.9")]
    public async Task Registro_xff_sin_confianza_no_salta_el_cupo(string? proxy)
    {
        using var api = new Api(proxy, ("LimitesAuth:RegistroLimiteVentana", "1"));
        Assert.Equal(HttpStatusCode.Created, (await api.Comprador("203.0.113.111")).StatusCode);
        await Es429(await api.Asesor("203.0.113.112"), MsgRegistro);
    }

    [Fact(DisplayName = "QA CA-P9-38, 43: el registro de comprador y el de asesor desde dos IPv6 del mismo /64 comparten cupo; otro /64 no; IPv4 mapeada = IPv4")]
    public async Task Registro_ipv6_por_64_e_ipv4_mapeada()
    {
        using var api = new Api("10.0.0.5", ("LimitesAuth:RegistroLimiteVentana", "2"));
        Assert.Equal(HttpStatusCode.Created, (await api.Comprador("2001:db8:7:1::10")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await api.Asesor("2001:db8:7:1:abcd::20")).StatusCode);
        var usuarios = await api.Leer(db => db.Users.CountAsync());
        await Es429(await api.Comprador("2001:db8:7:1:ffff:ffff:ffff:ffff"), MsgRegistro);
        Assert.Equal(usuarios, await api.Leer(db => db.Users.CountAsync()));
        Assert.Equal(HttpStatusCode.Created, (await api.Comprador("2001:db8:7:2::10")).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await api.Comprador("203.0.113.120")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await api.Asesor("::ffff:203.0.113.120")).StatusCode);
        await Es429(await api.Comprador("203.0.113.120"), MsgRegistro);
    }

    // ------------------------------------------------------------ CA-P9-41: 429 en el umbral del bloqueo

    [Fact(DisplayName = "QA CA-P9-41, 45: con 4 fallos acumulados (uno antes del bloqueo), el 429 no se cuenta como quinto fallo: no hay 423 ni LockoutEnd")]
    public async Task Login_429_en_el_umbral_no_bloquea()
    {
        using var api = new Api("10.0.0.5", ("LimitesAuth:LoginLimiteVentana", "4"));
        var email = CorreoUnico("umbral");
        Assert.Equal(HttpStatusCode.Created, (await api.Comprador("203.0.113.130", email)).StatusCode);
        const string ip = "203.0.113.131";
        for (var i = 0; i < 4; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await api.Login(ip, email, "Incorrecta2026")).StatusCode);
        for (var i = 0; i < 3; i++)
            await Es429(await api.Login(ip, email, "Incorrecta2026"), MsgLogin);

        var u = await api.Leer(db => db.Users.AsNoTracking().SingleAsync(x => x.Email == email));
        Assert.Equal(4, u.AccessFailedCount);
        Assert.Null(u.LockoutEnd);
        // Desde otra IP, la contraseña correcta entra (la cuenta no quedó bloqueada) y el contador se reinicia.
        Assert.Equal(HttpStatusCode.OK, (await api.Login("203.0.113.132", email, PasswordValida)).StatusCode);
        Assert.Equal(0, (await api.Leer(db => db.Users.AsNoTracking().SingleAsync(x => x.Email == email))).AccessFailedCount);
    }

    // ------------------------------------------------------------ CA-P9-38, 40: valores por defecto

    [Fact(DisplayName = "QA CA-P9-38 a 40 (§3.7): valores por defecto de LimitesAuth en el código y en appsettings.json: 5/60 min, 20/día, 20/15 min")]
    public void Valores_por_defecto()
    {
        var o = new OpcionesLimitesAuth();
        Assert.Equal((5, 60, 20, 20, 15),
            (o.RegistroLimiteVentana, o.RegistroVentanaMinutos, o.RegistroLimiteDiario, o.LoginLimiteVentana, o.LoginVentanaMinutos));

        // Ruta del archivo fuente (no de bin/, que puede estar en --artifacts-path fuera del repositorio).
        var ruta = Path.Combine(Path.GetDirectoryName(RutaFuente())!, "..", "Camion.Api", "appsettings.json");
        Assert.True(File.Exists(ruta), ruta);
        var config = new ConfigurationBuilder().AddJsonFile(ruta).Build();
        var desdeArchivo = config.GetSection("LimitesAuth").Get<OpcionesLimitesAuth>();
        Assert.NotNull(desdeArchivo);
        Assert.Equal((5, 60, 20, 20, 15),
            (desdeArchivo.RegistroLimiteVentana, desdeArchivo.RegistroVentanaMinutos, desdeArchivo.RegistroLimiteDiario,
                desdeArchivo.LoginLimiteVentana, desdeArchivo.LoginVentanaMinutos));
        Assert.Equal(PoliticaDatos.VersionPorDefecto, config[PoliticaDatos.Clave]);
    }

    [Fact(DisplayName = "QA CA-P9-38: con el valor de producción (5 por ventana) la sexta petición de registro de la misma IP -> 429")]
    public async Task Registro_sexta_peticion_con_valor_de_produccion()
    {
        using var api = new Api("10.0.0.5", ("LimitesAuth:RegistroLimiteVentana", "5"), ("LimitesAuth:RegistroLimiteDiario", "20"));
        const string ip = "203.0.113.140";
        for (var i = 0; i < 5; i++)
        {
            var r = i % 2 == 0 ? await api.Comprador(ip) : await api.Asesor(ip);
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        }
        await Es429(await api.Comprador(ip), MsgRegistro);
    }

    [Fact(DisplayName = "QA CA-P9-40: con el valor de producción (20 cada 15 min) la petición 21 de inicio de sesión -> 429")]
    public async Task Login_peticion_21_con_valor_de_produccion()
    {
        using var api = new Api("10.0.0.5", ("LimitesAuth:LoginLimiteVentana", "20"), ("LimitesAuth:LoginVentanaMinutos", "15"));
        const string ip = "203.0.113.150";
        for (var i = 0; i < 20; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await api.Login(ip, $"nadie{i}@ejemplo.test", "Incorrecta2026")).StatusCode);
        await Es429(await api.Login(ip, "nadie@ejemplo.test", "Incorrecta2026"), MsgLogin);
    }

    // ------------------------------------------------------------ CA-P9-44: el panel no tiene límite nuevo

    [Fact(DisplayName = "QA CA-P9-44: con los cupos de registro y de inicio de sesión agotados, las rutas con sesión (incluida /api/admin/parametros/empresa) no responden 429")]
    public async Task Panel_sin_limite_nuevo()
    {
        using var api = new Api("10.0.0.5", ("LimitesAuth:RegistroLimiteVentana", "1"), ("LimitesAuth:LoginLimiteVentana", "1"));
        const string ip = "203.0.113.160";
        var email = CorreoUnico("panel");
        Assert.Equal(HttpStatusCode.Created, (await api.Comprador(ip, email)).StatusCode);
        var login = await api.Login(ip, email, PasswordValida);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await Json(login)).GetProperty("accessToken").GetString()!;
        await Es429(await api.Comprador(ip), MsgRegistro);
        await Es429(await api.Login(ip, email, PasswordValida), MsgLogin);

        for (var i = 0; i < 25; i++)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await api.Get(RutaEmpresa, ip, token)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await api.Get("/api/portal/contacto", ip, null)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Get(RutaEmpresa, ip, null)).StatusCode);
    }

    // ------------------------------------------------------------ CA-P9-32: sesión no válida en la ruta de la empresa

    [Fact(DisplayName = "QA CA-P9-32: GET y PUT de la empresa con token alterado, no JWT o con token del administrador tras cerrar sesión -> 401; nada se escribe")]
    public async Task Empresa_sesion_no_valida()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var cuerpo = new { razonSocial = "Empresa Ficticia S.A.S.", nit = "800197268-4", direccion = "Calle 10 # 20-30" };
        var alterado = api.Admin.Token[..^4] + (api.Admin.Token.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");

        foreach (var metodo in new[] { HttpMethod.Get, HttpMethod.Put })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await api.Enviar(metodo, RutaEmpresa, alterado, cuerpo)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await api.Enviar(metodo, RutaEmpresa, "no-es-un-jwt", cuerpo)).StatusCode);
        }

        var otroAdmin = await CrearAdministrador(api.F, api.C);
        Assert.Equal(HttpStatusCode.NoContent, (await Logout(api.C, otroAdmin.Token)).StatusCode);
        foreach (var metodo in new[] { HttpMethod.Get, HttpMethod.Put })
            Assert.Equal(HttpStatusCode.Unauthorized, (await api.Enviar(metodo, RutaEmpresa, otroAdmin.Token, cuerpo)).StatusCode);

        Assert.Equal(0, await api.Leer(db => db.ConfiguracionesSistema.CountAsync(c => c.Clave == ParametrosEmpresa.Clave)));
        Assert.Equal(0, await api.Leer(db => db.AuditoriaEventos.CountAsync(a => a.EntidadId == ParametrosEmpresa.Clave)));
    }

    [Fact(DisplayName = "QA CA-P9-33: GET /api/portal/contacto es solo lectura (POST, PUT y DELETE no escriben la empresa)")]
    public async Task Portal_contacto_solo_lectura()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var cuerpo = new { empresa = new { razonSocial = "Intrusa S.A.S.", nit = "800197268-4", direccion = "Calle 1 # 2-3" } };
        foreach (var metodo in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete })
        {
            var r = await api.Enviar(metodo, "/api/portal/contacto", null, cuerpo);
            Assert.True((int)r.StatusCode is 405 or 404 or 401, $"{metodo}: {(int)r.StatusCode}");
        }
        Assert.Equal(JsonValueKind.Null, (await api.ContactoPublico()).GetProperty("empresa").ValueKind);
    }

    // ------------------------------------------------------------ CA-P9-29, 28, 30: bordes

    public static TheoryData<string, string?> CasosNit => new()
    {
        // Bordes de longitud de la base (6 y 10 dígitos, DV calculado a mano: 123456 -> 3; 1234567890 -> 2).
        { "123456-3", "123456-3" },
        { "1234567890-2", "1234567890-2" },
        { "123.456-3", "123456-3" },
        { "1.234.567.890-2", "1234567890-2" },
        { "800197268 - 4", "800197268-4" },
        { "123456-4", null },
        { "12345-5", null },
        { "12345678901-1", null },
        // Separadores y signos que no son el guion ASCII, o dígitos que no son ASCII.
        { "800197268–4", null },
        { "800197268_4", null },
        { "800,197,268-4", null },
        { "８００197268-4", null },
        { "-800197268-4", null },
        { "800197268-4-", null },
        { " - ", null },
        { "800197268-٤", null },
    };

    [Theory(DisplayName = "QA CA-P9-29 (UNIT): bordes del NIT (6 y 10 dígitos, separadores no admitidos, dígitos no ASCII)")]
    [MemberData(nameof(CasosNit))]
    public void Nit_bordes(string entrada, string? esperado) => Assert.Equal(esperado, ParametrosEmpresa.NormalizarNit(entrada));

    [Fact(DisplayName = "QA CA-P9-29 (UNIT): el DV de la DIAN de 123456 es 3 y el de 1234567890 es 2")]
    public void Digitos_de_bordes()
    {
        Assert.Equal(3, ParametrosEmpresa.DigitoVerificacion("123456"));
        Assert.Equal(2, ParametrosEmpresa.DigitoVerificacion("1234567890"));
    }

    [Fact(DisplayName = "QA CA-P9-28 a 30: bordes válidos por API (razón social de 2 y 150, dirección de 5 y 200, NIT de 6 y 10 dígitos)")]
    public async Task Bordes_validos_por_api()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        foreach (var (razon, nit, direccion) in new[]
                 {
                     ("SA", "123456-3", "Cl 12"),
                     (new string('R', 150), "1234567890-2", new string('D', 200)),
                 })
        {
            var r = await api.Enviar(HttpMethod.Put, RutaEmpresa, api.Admin.Token, new { razonSocial = razon, nit, direccion });
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var json = await Json(r);
            Assert.Equal(razon, json.GetProperty("razonSocial").GetString());
            Assert.Equal(nit, json.GetProperty("nit").GetString());
            Assert.Equal(direccion, json.GetProperty("direccion").GetString());
            Assert.True(json.GetProperty("completo").GetBoolean());
        }
    }

    [Fact(DisplayName = "QA CA-P9-31: un NIT bruto de más de 40 caracteres (con espacios) se rechaza con el mensaje del NIT, sin 500")]
    public async Task Nit_muy_largo()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        var r = await api.Enviar(HttpMethod.Put, RutaEmpresa, api.Admin.Token,
            new { razonSocial = (string?)null, nit = "800" + new string(' ', 5000) + "197268-4", direccion = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal([ParametrosEmpresa.MensajeNit], await ErroresDe(r, "nit"));
    }

    // ------------------------------------------------------------ CA-P9-08: 400 por autorización no cuenta como 409 ni filtra

    [Fact(DisplayName = "QA CA-P9-08, 10: sin autorización y con un correo ya registrado -> 400 (no 409): no se confirma si el correo existe antes de validar")]
    public async Task Sin_autorizacion_y_correo_repetido_da_400()
    {
        using var f = new CamionApiFactory();
        var c = f.CreateClient();
        var (_, email) = await CrearComprador(c, "Comprador Existente");
        var antes = await Db(f, db => db.ConsentimientosDatos.CountAsync());
        var r = await c.PostAsJsonAsync("/api/auth/registro/comprador",
            new { nombreCompleto = "Otra Persona", email, telefono = TelefonoValido, password = PasswordValida, autorizacionDatos = false });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal([ValidacionAuth.MensajeAutorizacionRegistro], await ErroresDe(r, "autorizacionDatos"));
        Assert.Equal(antes, await Db(f, db => db.ConsentimientosDatos.CountAsync()));
    }
    [Fact(DisplayName = "QA CA-P9-26: dos PUT simultáneos de la empresa sin fila previa -> ambos 200 (o uno 200 y otro reintentable), nunca 500; queda una sola fila")]
    public async Task Put_simultaneos_sin_fila()
    {
        using var api = await InventarioApi.Crear(NuevoReloj());
        const int paralelos = 4;
        using var barrera = new Barrier(paralelos);
        var respuestas = await Task.WhenAll(Enumerable.Range(0, paralelos).Select(i => Task.Run(async () =>
        {
            barrera.SignalAndWait();
            return await api.Enviar(HttpMethod.Put, RutaEmpresa, api.Admin.Token,
                new { razonSocial = $"Empresa Ficticia {i} S.A.S.", nit = "800197268-4", direccion = "Calle 10 # 20-30" });
        })));
        var codigos = string.Join(",", respuestas.Select(r => (int)r.StatusCode));
        Assert.True(respuestas.All(r => r.StatusCode != HttpStatusCode.InternalServerError), codigos);
        Assert.Equal(1, await api.Leer(db => db.ConfiguracionesSistema.CountAsync(c => c.Clave == ParametrosEmpresa.Clave)));
    }
}
