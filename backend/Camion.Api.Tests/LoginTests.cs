using System.Net;
using System.Text;
using Camion.Api.Data;
using Camion.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Camion.Api.Tests.Infraestructura.Ayudas;

namespace Camion.Api.Tests;

/// <summary>HU-059 Iniciar sesión (RN-SE-001) y HU-062 (no revelar qué falló).</summary>
public sealed class LoginTests(CamionApiFactory factory) : IClassFixture<CamionApiFactory>
{
    private readonly HttpClient _c = factory.CreateClient();

    private const string MsgCredenciales = "Correo o contraseña incorrectos.";
    private const string MsgFaltanDatos = "Escribe tu correo y tu contraseña.";
    private const string MsgBloqueo = "Cuenta bloqueada temporalmente por varios intentos fallidos. Intenta de nuevo en 15 minutos.";

    private async Task<string> CrearComprador()
    {
        var email = CorreoUnico();
        Assert.Equal(HttpStatusCode.Created, (await Registrar(_c, Registro(email, nombre: "Luis Prueba"))).StatusCode);
        return email;
    }

    [Fact(DisplayName = "CA-059-01/11: login correcto -> 200 con token Bearer, 2 h y roles; /me devuelve al propio usuario")]
    public async Task Login_correcto()
    {
        var email = await CrearComprador();
        var antes = DateTime.UtcNow;
        var r = await Login(_c, email, PasswordValida);

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var json = await Json(r);
        var token = json.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal("Bearer", json.GetProperty("tokenType").GetString());
        var expira = json.GetProperty("expiraEnUtc").GetDateTime().ToUniversalTime();
        Assert.InRange(expira, antes.AddHours(2).AddMinutes(-1), DateTime.UtcNow.AddHours(2).AddMinutes(1));
        Assert.Equal(email, json.GetProperty("email").GetString());
        Assert.Equal("Luis Prueba", json.GetProperty("nombreCompleto").GetString());
        Assert.Equal(["COMPRADOR"], json.GetProperty("roles").EnumerateArray().Select(x => x.GetString()!).ToArray());
        await AfirmarSinDatosSensibles(r, PasswordValida);

        var me = await Me(_c, token);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meJson = await Json(me);
        Assert.Equal(json.GetProperty("usuarioId").GetString(), meJson.GetProperty("usuarioId").GetString());
        Assert.Equal(email, meJson.GetProperty("email").GetString());
        Assert.Equal(TelefonoValido, meJson.GetProperty("phoneNumber").GetString());
        await AfirmarSinDatosSensibles(me, PasswordValida);
    }

    [Fact(DisplayName = "CA-059-02: el correo del login no distingue mayúsculas ni espacios alrededor")]
    public async Task Login_con_correo_en_mayusculas_y_espacios()
    {
        var email = await CrearComprador();
        var r = await Login(_c, "  " + email.ToUpperInvariant() + " ", PasswordValida);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact(DisplayName = "CA-059-03/04, CA-062-04: contraseña incorrecta y correo inexistente -> 401 con el mismo cuerpo")]
    public async Task Credenciales_incorrectas_mismo_mensaje()
    {
        var email = await CrearComprador();

        var malaPassword = await Login(_c, email, "Incorrecta2026");
        Assert.Equal(HttpStatusCode.Unauthorized, malaPassword.StatusCode);
        var cuerpo1 = await malaPassword.Content.ReadAsStringAsync();
        Assert.Equal(MsgCredenciales, (await Json(malaPassword)).GetProperty("error").GetString());

        var inexistente = await Login(_c, CorreoUnico("nadie"), "Incorrecta2026");
        Assert.Equal(HttpStatusCode.Unauthorized, inexistente.StatusCode);
        var cuerpo2 = await inexistente.Content.ReadAsStringAsync();

        Assert.Equal(cuerpo1, cuerpo2);
        await AfirmarSinDatosSensibles(malaPassword, "Incorrecta2026");
    }

    [Fact(DisplayName = "CA-059-05: cuenta no ACTIVA con contraseña correcta -> 401 con el mismo cuerpo")]
    public async Task Cuenta_no_activa_da_401()
    {
        var email = await CrearComprador();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CamionDbContext>();
            var u = await db.Users.SingleAsync(x => x.Email == email);
            u.EstadoCuenta = "SUSPENDIDA";
            await db.SaveChangesAsync();
        }
        var r = await Login(_c, email, PasswordValida);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Equal(MsgCredenciales, (await Json(r)).GetProperty("error").GetString());
    }

    [Theory(DisplayName = "CA-059-06: faltan correo o contraseña -> 400 con el mensaje del contrato")]
    [InlineData("", "")]
    [InlineData("", "Camion2026x")]
    [InlineData("alguien@ejemplo.test", "")]
    [InlineData("   ", "Camion2026x")]
    [InlineData(null, null)]
    [InlineData(null, "Camion2026x")]
    [InlineData("alguien@ejemplo.test", null)]
    public async Task Faltan_datos_da_400(string? email, string? password)
    {
        var r = await Login(_c, email, password);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal(MsgFaltanDatos, (await Json(r)).GetProperty("error").GetString());
    }

    [Theory(DisplayName = "CA-059-07: login con cuerpo vacío o JSON malformado -> 400, nunca 500")]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{ no es json")]
    [InlineData("{\"email\": 1, \"password\": false}")]
    public async Task Login_cuerpo_invalido_da_400(string cuerpo)
    {
        var r = await _c.PostAsync("/api/auth/login", new StringContent(cuerpo, Encoding.UTF8, "application/json"));
        Assert.NotEqual(HttpStatusCode.InternalServerError, r.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact(DisplayName = "CA-059-08, CA-061-04: 5 fallos -> 423; después incluso la contraseña correcta -> 423")]
    public async Task Cinco_fallos_bloquean()
    {
        var email = await CrearComprador();
        var codigos = new List<int>();
        for (var i = 0; i < 5; i++)
            codigos.Add((int)(await Login(_c, email, $"Incorrecta{i}A")).StatusCode);

        Assert.Equal([401, 401, 401, 401], codigos.Take(4));
        // Contrato: 423 desde el 5.º intento fallido inclusive.
        Assert.Equal(423, codigos[4]);

        var correcta = await Login(_c, email, PasswordValida);
        Assert.Equal(423, (int)correcta.StatusCode);
        Assert.Equal(MsgBloqueo, (await Json(correcta)).GetProperty("error").GetString());
        Assert.Contains(factory.Logs, l => l.Contains("Auditoria login bloqueado"));
    }

    [Fact(DisplayName = "CA-059-10: un login correcto reinicia el contador de fallos")]
    public async Task Login_correcto_reinicia_contador()
    {
        var email = await CrearComprador();
        for (var i = 0; i < 4; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Login(_c, email, "Incorrecta2026")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(_c, email, PasswordValida)).StatusCode);
        for (var i = 0; i < 4; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Login(_c, email, "Incorrecta2026")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(_c, email, PasswordValida)).StatusCode);
    }

    [Fact(DisplayName = "CA-059-09: cuenta bloqueada hace más de 15 minutos -> login correcto 200")]
    public async Task Bloqueo_vencido_permite_login()
    {
        var email = await CrearComprador();
        for (var i = 0; i < 5; i++) await Login(_c, email, "Incorrecta2026");
        Assert.Equal(423, (int)(await Login(_c, email, PasswordValida)).StatusCode);

        // Se simula el paso del tiempo moviendo LockoutEnd al pasado en la base de pruebas.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CamionDbContext>();
            var u = await db.Users.SingleAsync(x => x.Email == email);
            u.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await Login(_c, email, PasswordValida)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CamionDbContext>();
            Assert.Equal(0, (await db.Users.SingleAsync(x => x.Email == email)).AccessFailedCount);
        }
    }
}
