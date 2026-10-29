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

    [Fact(DisplayName = "CA-SE006-03/05/13, CA-061-15, CA-N03-R04, CA-SE005-06 (C-P3-8), CA-D03-05, P4a: anónimos solo registros, login, /health, los 4 GET del catálogo y GET /media; /api/admin exige SoloAdministrador; /api/inventario exige AsesorOAdministrador; ninguna ruta edita auditoría")]
    public async Task Inventario_de_endpoints()
    {
        var todos = Endpoints(factory.Services).ToList();
        var anonimos = todos.Where(e => e.Metadata.GetMetadata<IAuthorizeData>() is null)
            .Select(e => $"{Metodo(e)} /{e.RoutePattern.RawText!.TrimStart('/')}").OrderBy(x => x, StringComparer.Ordinal).ToArray();
        // P2: se añade el registro de asesor (anónimo, D-3). P3 (C-P3-8): las 4 rutas GET anónimas del catálogo público.
        // P4a (D-20): GET /media/{**ruta}, anónimo porque el catálogo público muestra las fotos.
        Assert.Equal(new[]
        {
            "GET /api/catalogo/filtros", "GET /api/catalogo/inicio", "GET /api/catalogo/publicaciones",
            "GET /api/catalogo/publicaciones/{slug}",
            "GET /health", "GET /health/database", "GET /media/{**ruta}", "POST /api/auth/login",
            "POST /api/auth/registro/asesor", "POST /api/auth/registro/comprador"
        }, anonimos);

        // P3: todo /api/catalogo es GET, permite anónimos de forma explícita y no exige ninguna política.
        var catalogo = todos.Where(e => e.RoutePattern.RawText!.TrimStart('/').StartsWith("api/catalogo", StringComparison.Ordinal)).ToList();
        Assert.Equal(4, catalogo.Count);
        foreach (var e in catalogo)
        {
            Assert.Equal("GET", Metodo(e));
            Assert.NotNull(e.Metadata.GetMetadata<IAllowAnonymous>());
            Assert.Empty(e.Metadata.GetOrderedMetadata<IAuthorizeData>());
        }

        // P4a (D-20): /media tiene una sola ruta, GET, anónima de forma explícita y sin política.
        var media = todos.Where(e => e.RoutePattern.RawText!.TrimStart('/').StartsWith("media", StringComparison.Ordinal)).ToList();
        var unicaMedia = Assert.Single(media);
        Assert.Equal("GET", Metodo(unicaMedia));
        Assert.NotNull(unicaMedia.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.Empty(unicaMedia.Metadata.GetOrderedMetadata<IAuthorizeData>());

        var modifican = todos.Where(e => Metodo(e) is "POST" or "PUT" or "PATCH" or "DELETE")
            .Select(e => $"{Metodo(e)} /{e.RoutePattern.RawText!.TrimStart('/')}").OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[]
        {
            "DELETE /api/inventario/vehiculos/{id}/fotos/{fotoId}",
            "POST /api/admin/usuarios/{id}/aprobar", "POST /api/admin/usuarios/{id}/bloquear", "POST /api/admin/usuarios/{id}/desbloquear",
            "POST /api/admin/usuarios/{id}/rechazar", "POST /api/admin/usuarios/{id}/reconsiderar",
            "POST /api/auth/login", "POST /api/auth/logout", "POST /api/auth/registro/asesor", "POST /api/auth/registro/comprador",
            // P4a: crear, 4 cambios de estado y subir foto.
            "POST /api/inventario/vehiculos", "POST /api/inventario/vehiculos/{id}/fotos",
            "POST /api/inventario/vehiculos/{id}/inactivar", "POST /api/inventario/vehiculos/{id}/liberar",
            "POST /api/inventario/vehiculos/{id}/reactivar", "POST /api/inventario/vehiculos/{id}/reservar",
            // P4a: editar y reordenar fotos.
            "PUT /api/inventario/vehiculos/{id}", "PUT /api/inventario/vehiculos/{id}/fotos/orden",
            // P4p: planes (crear, editar, activar, inactivar).
            "POST /api/admin/planes/", "PUT /api/admin/planes/{id}", "POST /api/admin/planes/{id}/activar",
            "POST /api/admin/planes/{id}/inactivar",
            // P4p: suscripciones (asignar, renovar, cambiar plan, suspender, reactivar, cancelar).
            "POST /api/admin/asesores/{asesorId}/suscripcion", "POST /api/admin/suscripciones/{id}/renovar",
            "POST /api/admin/suscripciones/{id}/cambiar-plan", "POST /api/admin/suscripciones/{id}/suspender",
            "POST /api/admin/suscripciones/{id}/reactivar", "POST /api/admin/suscripciones/{id}/cancelar",
            // P4p (D-37): catálogos (crear, renombrar, activar, desactivar) y modelos.
            "POST /api/admin/catalogos/{catalogo}", "PUT /api/admin/catalogos/{catalogo}/{id}",
            "POST /api/admin/catalogos/{catalogo}/{id}/activar", "POST /api/admin/catalogos/{catalogo}/{id}/desactivar",
            "POST /api/admin/catalogos/marcas/{marcaId}/lineas", "PUT /api/admin/catalogos/lineas/{id}",
            "POST /api/admin/catalogos/lineas/{id}/activar", "POST /api/admin/catalogos/lineas/{id}/desactivar"
        }.OrderBy(x => x, StringComparer.Ordinal).ToArray(), modifican);

        // CA-SE006-13: toda ruta bajo /api/admin exige SoloAdministrador (P2: 5 acciones + la lista; P4p: 4 GET y 18 acciones).
        var admin = todos.Where(e => e.RoutePattern.RawText!.TrimStart('/').StartsWith("api/admin", StringComparison.Ordinal)).ToList();
        Assert.Equal(28, admin.Count);
        foreach (var e in admin)
        {
            var politicas = e.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).ToArray();
            Assert.True(politicas.Contains("SoloAdministrador"),
                $"{Metodo(e)} {e.RoutePattern.RawText} no exige SoloAdministrador (políticas: {string.Join(",", politicas)})");
        }

        // P4a (RN-SE-006, D-7): toda ruta bajo /api/inventario exige AsesorOAdministrador y ninguna permite anónimos
        // (3 GET: lista, detalle y catálogos; 9 que modifican).
        var inventario = todos.Where(e => e.RoutePattern.RawText!.TrimStart('/').StartsWith("api/inventario", StringComparison.Ordinal)).ToList();
        Assert.Equal(12, inventario.Count);
        Assert.Equal(new[] { "/api/inventario/catalogos", "/api/inventario/vehiculos", "/api/inventario/vehiculos/{id}" },
            inventario.Where(e => Metodo(e) == "GET").Select(e => "/" + e.RoutePattern.RawText!.TrimStart('/'))
                .OrderBy(x => x, StringComparer.Ordinal).ToArray());
        foreach (var e in inventario)
        {
            var politicas = e.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).ToArray();
            Assert.True(politicas.Contains("AsesorOAdministrador"),
                $"{Metodo(e)} {e.RoutePattern.RawText} no exige AsesorOAdministrador (políticas: {string.Join(",", politicas)})");
            Assert.Null(e.Metadata.GetMetadata<IAllowAnonymous>());
        }

        // Las cuatro políticas con nombre existen.
        var proveedor = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        foreach (var nombre in new[] { "SoloAdministrador", "SoloAsesor", "Autenticado", "AsesorOAdministrador" })
            Assert.NotNull(await proveedor.GetPolicyAsync(nombre));

        // CA-061-15: ninguna ruta permite editar o borrar auditoría ni historial.
        Assert.DoesNotContain(todos, e => e.RoutePattern.RawText!.Contains("auditoria", StringComparison.OrdinalIgnoreCase) ||
                                          e.RoutePattern.RawText!.Contains("historial", StringComparison.OrdinalIgnoreCase));
        // Ninguna ruta usa PATCH; PUT solo en /api/inventario (P4a) y /api/admin (P4p); DELETE solo en /api/inventario
        // (P4p: planes y catálogos nunca se borran).
        Assert.DoesNotContain(todos, e => Metodo(e) == "PATCH");
        Assert.DoesNotContain(todos, e => Metodo(e) is "PUT" &&
            !e.RoutePattern.RawText!.TrimStart('/').StartsWith("api/inventario/", StringComparison.Ordinal) &&
            !e.RoutePattern.RawText!.TrimStart('/').StartsWith("api/admin/", StringComparison.Ordinal));
        Assert.DoesNotContain(todos, e => Metodo(e) is "DELETE" &&
            !e.RoutePattern.RawText!.TrimStart('/').StartsWith("api/inventario/", StringComparison.Ordinal));

        // P4p (HU-066): /api/asesor/suscripcion es la única ruta /api/asesor y exige SoloAsesor.
        var asesor = Assert.Single(todos, e => e.RoutePattern.RawText!.TrimStart('/').StartsWith("api/asesor", StringComparison.Ordinal));
        Assert.Equal("GET", Metodo(asesor));
        Assert.Contains("SoloAsesor", asesor.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy));

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
