using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Camion.Api.Auth;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;

namespace Camion.Api.Endpoints;

public static class AuthEndpoints
{
    public const string Emisor = "Camion.Api";
    public const string Audiencia = "Camion.Frontend";
    public const string ClaimSessionVersion = "session_version";
    public const string RolComprador = "COMPRADOR";
    public const string CategoriaAuditoria = "Camion.Api.Auditoria";

    private const string MensajeCredenciales = "Correo o contraseña incorrectos.";
    private const string MensajeDuplicado = "Este correo ya está registrado.";
    private const string MensajeBloqueo = "Cuenta bloqueada temporalmente por varios intentos fallidos. Intenta de nuevo en 15 minutos.";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app, string jwtKey)
    {
        var auth = app.MapGroup("/api/auth");

        // HU-N03, D-3, D-5: registro público, rol fijo COMPRADOR y acceso inmediato.
        auth.MapPost("/registro/comprador", async (
            RegistroCompradorRequest? request,
            UserManager<ApplicationUser> users,
            RoleManager<IdentityRole> roles,
            CamionDbContext db,
            ILoggerFactory loggerFactory) =>
        {
            var log = loggerFactory.CreateLogger(CategoriaAuditoria);
            request ??= new RegistroCompradorRequest(null, null, null, null);
            var errores = ValidacionAuth.ValidarRegistro(request);
            if (errores.Count > 0) return Validacion(errores);

            var email = ValidacionAuth.NormalizarEmail(request.Email);
            if (await users.FindByEmailAsync(email) is not null)
                return Results.Conflict(new ErrorResponse(MensajeDuplicado));

            try
            {
                // El rol se crea una sola vez si aún no existe.
                if (!await roles.RoleExistsAsync(RolComprador))
                {
                    var roleResult = await roles.CreateAsync(new IdentityRole(RolComprador));
                    if (!roleResult.Succeeded && !await roles.RoleExistsAsync(RolComprador))
                        return Results.Problem("No se pudo preparar el rol comprador.");
                }
            }
            catch (DbUpdateException)
            {
                // Otro registro simultáneo creó el rol.
                db.ChangeTracker.Clear();
                if (!await roles.RoleExistsAsync(RolComprador))
                    return Results.Problem("No se pudo preparar el rol comprador.");
            }

            await using var tx = await db.Database.BeginTransactionAsync();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = email,
                Email = email,
                PhoneNumber = request.Telefono!.Trim(),
                NombreCompleto = request.NombreCompleto!.Trim()
            };
            try
            {
                var created = await users.CreateAsync(user, request.Password!);
                if (!created.Succeeded)
                {
                    await tx.RollbackAsync();
                    if (ValidacionAuth.EsDuplicado(created.Errors))
                        return Results.Conflict(new ErrorResponse(MensajeDuplicado));
                    return Validacion(ValidacionAuth.TraducirErroresIdentity(created.Errors));
                }
                var assigned = await users.AddToRoleAsync(user, RolComprador);
                if (!assigned.Succeeded)
                {
                    await tx.RollbackAsync();
                    return Results.Problem("No fue posible asignar el rol comprador.");
                }
                db.PerfilesComprador.Add(new PerfilComprador { UsuarioId = user.Id });
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync();
                db.ChangeTracker.Clear();
                // Dos registros simultáneos con el mismo correo chocan en el índice único: 409, nunca 500.
                if (ex.InnerException is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry } ||
                    await users.FindByEmailAsync(email) is not null)
                    return Results.Conflict(new ErrorResponse(MensajeDuplicado));
                throw;
            }

            log.LogInformation("Auditoria registro_comprador exitoso usuarioId={UsuarioId}", user.Id);
            return Results.Created("/api/auth/me",
                new RegistroCompradorResponse(user.Id, "Cuenta creada. Ya puedes iniciar sesión."));
        });

        // HU-059, RN-SE-001, HU-062: mismo mensaje para cualquier fallo de credenciales.
        auth.MapPost("/login", async (
            LoginRequest? request,
            UserManager<ApplicationUser> users,
            SignInManager<ApplicationUser> signIn,
            ILoggerFactory loggerFactory) =>
        {
            var log = loggerFactory.CreateLogger(CategoriaAuditoria);
            var email = ValidacionAuth.NormalizarEmail(request?.Email);
            var password = request?.Password ?? "";
            if (email.Length == 0 || password.Length == 0)
                return Results.BadRequest(new ErrorResponse("Escribe tu correo y tu contraseña."));

            var user = await users.FindByEmailAsync(email);
            if (user is null || user.EstadoCuenta != "ACTIVA")
            {
                log.LogWarning("Auditoria login fallido motivo={Motivo} usuarioId={UsuarioId} correo={Correo}",
                    user is null ? "usuario_inexistente" : "cuenta_no_activa", user?.Id, ValidacionAuth.EnmascararEmail(email));
                return Credenciales();
            }

            var check = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
            if (check.IsLockedOut)
            {
                log.LogWarning("Auditoria login bloqueado usuarioId={UsuarioId}", user.Id);
                return Results.Json(new ErrorResponse(MensajeBloqueo), statusCode: StatusCodes.Status423Locked);
            }
            if (!check.Succeeded)
            {
                log.LogWarning("Auditoria login fallido motivo=contrasena_incorrecta usuarioId={UsuarioId}", user.Id);
                return Credenciales();
            }

            var userRoles = await users.GetRolesAsync(user);
            var expiry = DateTime.UtcNow.AddHours(2);
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.NombreCompleto),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(ClaimSessionVersion, user.SessionVersion.ToString())
            };
            claims.AddRange(userRoles.Select(role => new Claim(ClaimTypes.Role, role)));
            var token = new JwtSecurityToken(
                issuer: Emisor,
                audience: Audiencia,
                claims: claims,
                expires: expiry,
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), SecurityAlgorithms.HmacSha256));
            user.UltimoAccesoEn = DateTime.UtcNow;
            await users.UpdateAsync(user);
            log.LogInformation("Auditoria login exitoso usuarioId={UsuarioId}", user.Id);
            return Results.Ok(new AuthResponse(
                new JwtSecurityTokenHandler().WriteToken(token), "Bearer", expiry,
                user.Id, user.NombreCompleto, user.Email ?? "", userRoles.ToArray()));
        });

        // La sesión (session_version y EstadoCuenta) ya se validó en OnTokenValidated.
        auth.MapGet("/me", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users) =>
        {
            var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = id is null ? null : await users.FindByIdAsync(id);
            if (user is null) return Results.Unauthorized();
            var userRoles = await users.GetRolesAsync(user);
            return Results.Ok(new MeResponse(user.Id, user.NombreCompleto, user.Email, user.PhoneNumber, userRoles.ToArray()));
        }).RequireAuthorization();

        // HU-060, RN-SE-002: invalida todos los tokens emitidos antes para el usuario.
        auth.MapPost("/logout", async (
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> users,
            CamionDbContext db,
            ILoggerFactory loggerFactory) =>
        {
            var log = loggerFactory.CreateLogger(CategoriaAuditoria);
            var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = id is null ? null : await users.FindByIdAsync(id);
            if (user is null) return Results.Unauthorized();
            for (var intento = 0; intento < 3; intento++)
            {
                user.SessionVersion++;
                var updated = await users.UpdateAsync(user);
                if (updated.Succeeded)
                {
                    log.LogInformation("Auditoria logout usuarioId={UsuarioId}", user.Id);
                    return Results.NoContent();
                }
                // Conflicto de concurrencia (otro cambio simultáneo): se recarga y se reintenta.
                await db.Entry(user).ReloadAsync();
            }
            return Results.Problem("No fue posible cerrar la sesión. Intenta de nuevo.");
        }).RequireAuthorization();

        return app;
    }

    private static IResult Credenciales() =>
        Results.Json(new ErrorResponse(MensajeCredenciales), statusCode: StatusCodes.Status401Unauthorized);

    private static IResult Validacion(Dictionary<string, string[]> errores) =>
        Results.BadRequest(new ErrorResponse("Revisa los datos marcados.", errores));
}
