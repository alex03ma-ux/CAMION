using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Camion.Api.Data;
using Camion.Api.Endpoints;
using Camion.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using static Camion.Api.Tests.Infraestructura.Ayudas;

namespace Camion.Api.Tests.Infraestructura;

/// <summary>
/// Ayudas de P2 (asesores y administración de usuarios). Datos ficticios; la base es siempre la SQLite en memoria
/// de <see cref="CamionApiFactory"/>. El administrador se crea directamente en la base de pruebas (contrato P2, D-13).
/// </summary>
public static class AyudasP2
{
    public const string MsgSinPermiso = "No tienes permiso para realizar esta acción.";
    public const string MsgCuentaAdmin = "No se puede modificar una cuenta de administrador.";
    public const string MsgNoEncontrado = "Usuario no encontrado.";
    public const string MsgMotivo = "Escribe el motivo (5 a 500 caracteres).";
    public const string MsgSolicitud = "Solicitud enviada. Podrás iniciar sesión cuando el administrador apruebe tu cuenta.";
    public const string MsgPendiente = "Tu cuenta de asesor está pendiente de aprobación. Te avisaremos cuando el administrador la apruebe.";
    public const string MsgRechazada = "Tu solicitud de asesor no fue aprobada. Comunícate con el administrador.";
    public const string MsgBloqueada = "Tu cuenta está bloqueada. Comunícate con el administrador.";
    public const string MsgDocumentoDuplicado = "Ya existe un asesor con ese documento.";
    public const string MsgCorreoDuplicado = "Este correo ya está registrado.";
    public const string MotivoValido = "Motivo de prueba";

    public static readonly string[] Acciones = ["aprobar", "rechazar", "bloquear", "desbloquear", "reconsiderar"];

    /// <summary>Número de documento ficticio y único (solo dígitos, 10 caracteres).</summary>
    public static string DocumentoUnico() =>
        string.Concat(Guid.NewGuid().ToString("N").Where(char.IsDigit).Concat("0123456789")).Substring(0, 10);

    public static Dictionary<string, object?> RegistroAsesor(string? email = null, string? numero = null,
        string? tipo = "CC", string? nombre = "Carlos Asesor", string? comercial = "Camiones de Prueba")
        => new()
        {
            ["nombreCompleto"] = nombre,
            ["email"] = email ?? CorreoUnico("asesor"),
            ["telefono"] = TelefonoValido,
            ["password"] = PasswordValida,
            ["tipoDocumento"] = tipo,
            ["numeroDocumento"] = numero ?? DocumentoUnico(),
            ["nombreComercial"] = comercial
        };

    public static Task<HttpResponseMessage> RegistrarAsesor(HttpClient c, object cuerpo) =>
        c.PostAsJsonAsync("/api/auth/registro/asesor", cuerpo);

    public static async Task<(string Id, string Email, string Numero)> CrearAsesorPendiente(HttpClient c, string? nombre = null)
    {
        var email = CorreoUnico("asesor");
        var numero = DocumentoUnico();
        var r = await RegistrarAsesor(c, RegistroAsesor(email, numero, nombre: nombre ?? "Carlos Asesor"));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return ((await Json(r)).GetProperty("usuarioId").GetString()!, email, numero);
    }

    public static async Task<(string Id, string Email)> CrearComprador(HttpClient c, string nombre = "Ana Prueba")
    {
        var email = CorreoUnico();
        var r = await Registrar(c, Registro(email, nombre: nombre));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return ((await Json(r)).GetProperty("usuarioId").GetString()!, email);
    }

    /// <summary>Crea un administrador ACTIVA directamente en la base en memoria y devuelve su token.</summary>
    public static async Task<(string Id, string Email, string Token)> CrearAdministrador(CamionApiFactory f, HttpClient c)
    {
        var email = CorreoUnico("admin");
        string id;
        using (var scope = f.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync("ADMINISTRADOR"))
                Assert.True((await roles.CreateAsync(new IdentityRole("ADMINISTRADOR"))).Succeeded);
            var u = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = email,
                Email = email,
                PhoneNumber = TelefonoValido,
                NombreCompleto = "Admin Prueba",
                EstadoCuenta = EstadosCuenta.Activa
            };
            Assert.True((await users.CreateAsync(u, PasswordValida)).Succeeded);
            Assert.True((await users.AddToRoleAsync(u, "ADMINISTRADOR")).Succeeded);
            id = u.Id;
        }
        return (id, email, await ObtenerToken(c, email));
    }

    public static Task<HttpResponseMessage> Accion(HttpClient c, string? token, string id, string accion, object? cuerpo)
    {
        var req = ConToken(HttpMethod.Post, $"/api/admin/usuarios/{Uri.EscapeDataString(id)}/{accion}", token);
        req.Content = JsonContent.Create(cuerpo ?? new { });
        return c.SendAsync(req);
    }

    public static Task<HttpResponseMessage> AccionMotivo(HttpClient c, string? token, string id, string accion, string? motivo) =>
        Accion(c, token, id, accion, new { motivo });

    public static Task<HttpResponseMessage> Lista(HttpClient c, string? token, string query = "") =>
        c.SendAsync(ConToken(HttpMethod.Get, "/api/admin/usuarios" + query, token));

    public static async Task<JsonElement> Ok(HttpResponseMessage r)
    {
        var texto = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == HttpStatusCode.OK, $"Se esperaba 200 y llegó {(int)r.StatusCode}: {texto}");
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    public static async Task AfirmarError(HttpResponseMessage r, HttpStatusCode codigo, string mensaje)
    {
        var texto = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == codigo, $"Se esperaba {(int)codigo} y llegó {(int)r.StatusCode}: {texto}");
        Assert.Equal(mensaje, JsonDocument.Parse(texto).RootElement.GetProperty("error").GetString());
    }

    /// <summary>CA-048-03: ningún campo interno de Identity en la respuesta.</summary>
    public static async Task AfirmarSinCamposInternos(HttpResponseMessage r)
    {
        var texto = await r.Content.ReadAsStringAsync();
        foreach (var campo in new[] { "passwordHash", "securityStamp", "concurrencyStamp", "sessionVersion",
                     "accessFailedCount", "lockoutEnd", "lockoutEnabled", "normalizedEmail", "normalizedUserName",
                     "twoFactorEnabled", "emailConfirmed", "phoneNumberConfirmed", "autorizadoPor" })
            Assert.DoesNotContain("\"" + campo + "\"", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(PasswordValida, texto, StringComparison.Ordinal);
    }

    public static async Task<T> Db<T>(CamionApiFactory f, Func<CamionDbContext, Task<T>> consulta)
    {
        using var scope = f.Services.CreateScope();
        return await consulta(scope.ServiceProvider.GetRequiredService<CamionDbContext>());
    }

    public static Task<ApplicationUser> Usuario(CamionApiFactory f, string id) =>
        Db(f, db => db.Users.AsNoTracking().SingleAsync(u => u.Id == id));

    public static Task<PerfilAsesor?> Perfil(CamionApiFactory f, string usuarioId) =>
        Db(f, db => db.PerfilesAsesor.AsNoTracking().SingleOrDefaultAsync(p => p.UsuarioId == usuarioId));

    public static Task<List<AuditoriaEvento>> AuditoriaDe(CamionApiFactory f, string usuarioId) =>
        Db(f, db => db.AuditoriaEventos.AsNoTracking().Where(a => a.EntidadId == usuarioId).OrderBy(a => a.Id).ToListAsync());

    public static Task<List<HistorialAutorizacion>> HistorialDe(CamionApiFactory f, string usuarioId) =>
        Db(f, db => (from h in db.HistorialAutorizaciones.AsNoTracking()
                     join p in db.PerfilesAsesor on h.AsesorId equals p.Id
                     where p.UsuarioId == usuarioId
                     orderby h.Id
                     select h).ToListAsync());

    public static Task<string[]> RolesDe(CamionApiFactory f, string usuarioId) =>
        Db(f, async db => (await (from ur in db.UserRoles
                                  join r in db.Roles on ur.RoleId equals r.Id
                                  where ur.UserId == usuarioId
                                  select r.Name!).ToListAsync()).OrderBy(x => x, StringComparer.Ordinal).ToArray());

    public static async Task CambiarEstado(CamionApiFactory f, string id, string estado)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CamionDbContext>();
        (await db.Users.SingleAsync(u => u.Id == id)).EstadoCuenta = estado;
        await db.SaveChangesAsync();
    }

    /// <summary>Token firmado con la clave de pruebas para un usuario y una SessionVersion dadas (simula una sesión previa).</summary>
    public static string Firmar(CamionApiFactory f, string usuarioId, int sessionVersion, string rol)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuarioId),
            new Claim(ClaimTypes.NameIdentifier, usuarioId),
            new Claim(AuthEndpoints.ClaimSessionVersion, sessionVersion.ToString()),
            new Claim(ClaimTypes.Role, rol)
        };
        var token = new JwtSecurityToken(AuthEndpoints.Emisor, AuthEndpoints.Audiencia, claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1), expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(f.JwtKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static Dictionary<string, string?> JsonPlano(string? json) =>
        json is null ? new() : JsonSerializer.Deserialize<Dictionary<string, string?>>(json)!;
}
