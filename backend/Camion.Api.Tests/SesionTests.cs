using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Camion.Api.Data;
using Camion.Api.Endpoints;
using Camion.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using static Camion.Api.Tests.Infraestructura.Ayudas;

namespace Camion.Api.Tests;

/// <summary>HU-059 /me, HU-060 logout y caducidad, RN-SE-006 control de accesos, HU-061/062.</summary>
public sealed class SesionTests(CamionApiFactory factory) : IClassFixture<CamionApiFactory>
{
    private readonly HttpClient _c = factory.CreateClient();

    private async Task<(string Id, int SessionVersion)> Usuario(string email)
    {
        using var scope = factory.Services.CreateScope();
        var u = await scope.ServiceProvider.GetRequiredService<CamionDbContext>().Users.SingleAsync(x => x.Email == email);
        return (u.Id, u.SessionVersion);
    }

    private string Firmar(string usuarioId, int sessionVersion, DateTime expira, string? clave = null,
        string emisor = AuthEndpoints.Emisor, string audiencia = AuthEndpoints.Audiencia, DateTime? noAntes = null)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuarioId),
            new Claim(ClaimTypes.NameIdentifier, usuarioId),
            new Claim(AuthEndpoints.ClaimSessionVersion, sessionVersion.ToString()),
            new Claim(ClaimTypes.Role, "COMPRADOR")
        };
        var token = new JwtSecurityToken(emisor, audiencia, claims, notBefore: noAntes ?? expira.AddHours(-2), expires: expira,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave ?? factory.JwtKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static IEnumerable<RouteEndpoint> Endpoints(IServiceProvider sp) =>
        sp.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

    private static string Metodo(RouteEndpoint e) =>
        e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Single() ?? "GET";

    [Fact(DisplayName = "CA-059-12, CA-SE006-03: /me y /logout sin token -> 401")]
    public async Task Sin_token_da_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Me(_c, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Logout(_c, null)).StatusCode);
    }

    [Fact(DisplayName = "CA-059-12, CA-SE006-04: token con firma alterada -> 401")]
    public async Task Token_manipulado_da_401()
    {
        var token = await RegistrarYObtenerToken(_c);
        var partes = token.Split('.');
        // Se altera la firma.
        var firma = partes[2].ToCharArray();
        firma[5] = firma[5] == 'A' ? 'B' : 'A';
        var firmaAlterada = $"{partes[0]}.{partes[1]}.{new string(firma)}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await Me(_c, firmaAlterada)).StatusCode);

        // Se altera la carga útil (p. ej. para añadir un rol) sin volver a firmar.
        var carga = Base64UrlEncoder.Decode(partes[1]).Replace("COMPRADOR", "ADMINISTRADOR");
        var cargaAlterada = $"{partes[0]}.{Base64UrlEncoder.Encode(carga)}.{partes[2]}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await Me(_c, cargaAlterada)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Me(_c, "no-es-un-jwt")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Me(_c, token)).StatusCode); // el original sigue valiendo
    }

    [Fact(DisplayName = "CA-SE006-04: token firmado con otra clave, otro emisor u otra audiencia -> 401")]
    public async Task Token_de_otro_origen_da_401()
    {
        var email = CorreoUnico();
        await RegistrarYObtenerToken(_c, email);
        var (id, version) = await Usuario(email);
        var expira = DateTime.UtcNow.AddHours(1);

        Assert.Equal(HttpStatusCode.OK, (await Me(_c, Firmar(id, version, expira))).StatusCode); // control positivo
        var otraClave = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Me(_c, Firmar(id, version, expira, clave: otraClave))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Me(_c, Firmar(id, version, expira, emisor: "otro"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Me(_c, Firmar(id, version, expira, audiencia: "otra"))).StatusCode);
    }

    [Fact(DisplayName = "CA-059-12, CA-060-07: token caducado (más de 2 h + 30 s) -> 401")]
    public async Task Token_caducado_da_401()
    {
        var email = CorreoUnico();
        await RegistrarYObtenerToken(_c, email);
        var (id, version) = await Usuario(email);
        var caducado = Firmar(id, version, DateTime.UtcNow.AddSeconds(-31), noAntes: DateTime.UtcNow.AddHours(-2).AddSeconds(-31));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Me(_c, caducado)).StatusCode);
    }

    [Fact(DisplayName = "CA-060-01/02/04, CA-061-05: logout 204; el mismo token -> 401 en /me y /logout; login nuevo funciona")]
    public async Task Logout_invalida_token()
    {
        var email = CorreoUnico();
        var token = await RegistrarYObtenerToken(_c, email);
        var (_, antes) = await Usuario(email);

        var r = await Logout(_c, token);
        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
        Assert.Equal("", await r.Content.ReadAsStringAsync());
        Assert.Equal(antes + 1, (await Usuario(email)).SessionVersion);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Me(_c, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Logout(_c, token)).StatusCode);

        var nuevo = await ObtenerToken(_c, email);
        Assert.Equal(HttpStatusCode.OK, (await Me(_c, nuevo)).StatusCode);
        Assert.Contains(factory.Logs, l => l.Contains("Auditoria logout"));
    }

    [Fact(DisplayName = "CA-060-03: logout desde un dispositivo invalida también el token del otro")]
    public async Task Logout_cierra_todos_los_dispositivos()
    {
        var email = CorreoUnico();
        var token1 = await RegistrarYObtenerToken(_c, email);
        var token2 = await ObtenerToken(_c, email);
        Assert.Equal(HttpStatusCode.OK, (await Me(_c, token2)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await Logout(_c, token1)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Me(_c, token2)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Logout(_c, token2)).StatusCode);
    }

    [Fact(DisplayName = "CA-060-05: el logout de un usuario no afecta al token de otro")]
    public async Task Logout_no_afecta_a_otro_usuario()
    {
        var tokenA = await RegistrarYObtenerToken(_c);
        var tokenB = await RegistrarYObtenerToken(_c);
        Assert.Equal(HttpStatusCode.NoContent, (await Logout(_c, tokenA)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Me(_c, tokenB)).StatusCode);
    }

    [Fact(DisplayName = "CA-060-06: logout con token inválido -> 401")]
    public async Task Logout_token_invalido_da_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Logout(_c, "x.y.z")).StatusCode);
    }

    [Fact(DisplayName = "CA-SE006-01: un token con session_version anterior -> 401 en TODOS los endpoints protegidos")]
    public async Task Session_version_antigua_rechazada_en_todos_los_protegidos()
    {
        var email = CorreoUnico();
        var viejo = await RegistrarYObtenerToken(_c, email);
        Assert.Equal(HttpStatusCode.NoContent, (await Logout(_c, await ObtenerToken(_c, email))).StatusCode);

        var protegidos = Endpoints(factory.Services)
            .Where(e => e.Metadata.GetMetadata<IAuthorizeData>() is not null).ToList();
        Assert.NotEmpty(protegidos);
        foreach (var e in protegidos)
        {
            var r = await _c.SendAsync(ConToken(new HttpMethod(Metodo(e)), "/" + e.RoutePattern.RawText!.TrimStart('/'), viejo));
            Assert.True(r.StatusCode == HttpStatusCode.Unauthorized, $"{Metodo(e)} {e.RoutePattern.RawText} devolvió {(int)r.StatusCode}");
        }
    }

    [Fact(DisplayName = "CA-SE006-02: token vigente de una cuenta que pasa a no ACTIVA -> 401 en todos los protegidos")]
    public async Task Cuenta_no_activa_rechazada_en_todos_los_protegidos()
    {
        var email = CorreoUnico();
        var token = await RegistrarYObtenerToken(_c, email);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CamionDbContext>();
            (await db.Users.SingleAsync(x => x.Email == email)).EstadoCuenta = "SUSPENDIDA";
            await db.SaveChangesAsync();
        }
        foreach (var e in Endpoints(factory.Services).Where(e => e.Metadata.GetMetadata<IAuthorizeData>() is not null))
        {
            var r = await _c.SendAsync(ConToken(new HttpMethod(Metodo(e)), "/" + e.RoutePattern.RawText!.TrimStart('/'), token));
            Assert.True(r.StatusCode == HttpStatusCode.Unauthorized, $"{Metodo(e)} {e.RoutePattern.RawText} devolvió {(int)r.StatusCode}");
        }
    }

    [Fact(DisplayName = "CA-SE006-03/05: anónimos solo registro, login y /health; únicos endpoints que modifican datos: registro, login, logout")]
    public async Task Inventario_de_endpoints()
    {
        var todos = Endpoints(factory.Services).ToList();
        var anonimos = todos.Where(e => e.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(e => $"{Metodo(e)} /{e.RoutePattern.RawText!.TrimStart('/')}").OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "GET /health", "GET /health/database", "POST /api/auth/login", "POST /api/auth/registro/comprador" }, anonimos);

        var modifican = todos.Where(e => Metodo(e) is "POST" or "PUT" or "PATCH" or "DELETE")
            .Select(e => "/" + e.RoutePattern.RawText!.TrimStart('/')).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "/api/auth/login", "/api/auth/logout", "/api/auth/registro/comprador" }, modifican);

        // /health es anónimo (no se prueba /health/database: intentaría conectar con MariaDB).
        Assert.Equal(HttpStatusCode.OK, (await _c.GetAsync("/health")).StatusCode);
    }

    [Fact(DisplayName = "CA-061-01/02/03/06: auditoría en el log sin contraseñas, tokens ni correo completo de fallos")]
    public async Task Auditoria_sin_datos_sensibles()
    {
        var email = CorreoUnico("auditoria");
        var r = await Registrar(_c, Registro(email));
        var id = (await Json(r)).GetProperty("usuarioId").GetString()!;
        var login = await Login(_c, email, PasswordValida);
        var token = (await Json(login)).GetProperty("accessToken").GetString()!;
        await Login(_c, email, "Incorrecta2026");
        var inexistente = CorreoUnico("inexistente");
        await Login(_c, inexistente, "Incorrecta2026");
        await Logout(_c, token);

        var logs = factory.Logs.ToArray();
        Assert.Contains(logs, l => l.Contains("Auditoria registro_comprador exitoso") && l.Contains(id));
        Assert.Contains(logs, l => l.Contains("Auditoria login exitoso") && l.Contains(id));
        Assert.Contains(logs, l => l.Contains("Auditoria login fallido") && l.Contains("contrasena_incorrecta") && l.Contains(id));
        Assert.Contains(logs, l => l.Contains("Auditoria login fallido") && l.Contains("usuario_inexistente"));
        Assert.Contains(logs, l => l.Contains("Auditoria logout") && l.Contains(id));

        foreach (var l in logs)
        {
            Assert.DoesNotContain(PasswordValida, l);
            Assert.DoesNotContain("Incorrecta2026", l);
            Assert.DoesNotContain(token, l);
            Assert.DoesNotContain(inexistente, l);
        }
    }

    [Fact(DisplayName = "CA-062-03: /me solo devuelve los datos del dueño del token")]
    public async Task Me_solo_datos_propios()
    {
        var emailA = CorreoUnico("a");
        var emailB = CorreoUnico("b");
        var tokenA = await RegistrarYObtenerToken(_c, emailA);
        await RegistrarYObtenerToken(_c, emailB);
        var me = await Json(await Me(_c, tokenA));
        Assert.Equal(emailA, me.GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await _c.SendAsync(ConToken(HttpMethod.Get, "/api/auth/me/otro", tokenA))).StatusCode);
    }
}
