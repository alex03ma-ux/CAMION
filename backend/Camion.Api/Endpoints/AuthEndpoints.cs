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
    public const string RolComprador = Roles.Comprador;
    public const string CategoriaAuditoria = "Camion.Api.Auditoria";

    private const string MensajeCredenciales = "Correo o contraseña incorrectos.";
    private const string MensajeDuplicado = "Este correo ya está registrado.";
    private const string MensajeBloqueo = "Cuenta bloqueada temporalmente por varios intentos fallidos. Intenta de nuevo en 15 minutos.";
    private const string MensajeDocumentoDuplicado = "Ya existe un asesor con ese documento.";
    private const string MensajeSolicitudAsesor = "Solicitud enviada. Podrás iniciar sesión cuando el administrador apruebe tu cuenta.";
    private const string MensajePendiente = "Tu cuenta de asesor está pendiente de aprobación. Te avisaremos cuando el administrador la apruebe.";
    private const string MensajeRechazada = "Tu solicitud de asesor no fue aprobada. Comunícate con el administrador.";
    private const string MensajeBloqueada = "Tu cuenta está bloqueada. Comunícate con el administrador.";

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

            // El rol se crea una sola vez si aún no existe (tolera que otro registro simultáneo lo cree).
            if (!await Roles.AsegurarAsync(roles, db, RolComprador))
                return ErrorInterno();

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
                    return ErrorInterno();
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
                throw; // Lo atiende el manejador global: 500 con { error }.
            }

            log.LogInformation("Auditoria registro_comprador exitoso usuarioId={UsuarioId}", user.Id);
            return Results.Created("/api/auth/me",
                new RegistroCompradorResponse(user.Id, "Cuenta creada. Ya puedes iniciar sesión."));
        });

        // P2 (HU-043, D-3, D-5): registro público de asesor; rol fijo ASESOR y cuenta PENDIENTE hasta su aprobación.
        auth.MapPost("/registro/asesor", async (
            RegistroAsesorRequest? request,
            UserManager<ApplicationUser> users,
            RoleManager<IdentityRole> roles,
            CamionDbContext db,
            ILoggerFactory loggerFactory) =>
        {
            var log = loggerFactory.CreateLogger(CategoriaAuditoria);
            request ??= new RegistroAsesorRequest(null, null, null, null, null, null, null);
            var errores = ValidacionAuth.ValidarRegistroAsesor(request);
            if (errores.Count > 0) return Validacion(errores);

            var email = ValidacionAuth.NormalizarEmail(request.Email);
            var tipo = ValidacionAuth.NormalizarTipoDocumento(request.TipoDocumento);
            var numero = ValidacionAuth.NormalizarNumeroDocumento(request.NumeroDocumento);
            if (await users.FindByEmailAsync(email) is not null)
                return Results.Conflict(new ErrorResponse(MensajeDuplicado));
            if (await DocumentoRegistrado(db, tipo, numero))
                return DocumentoDuplicado();

            if (!await Roles.AsegurarAsync(roles, db, Roles.Asesor))
                return ErrorInterno();

            await using var tx = await db.Database.BeginTransactionAsync();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = email,
                Email = email,
                PhoneNumber = request.Telefono!.Trim(),
                NombreCompleto = request.NombreCompleto!.Trim(),
                EstadoCuenta = EstadosCuenta.Pendiente
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
                var assigned = await users.AddToRoleAsync(user, Roles.Asesor);
                if (!assigned.Succeeded)
                {
                    await tx.RollbackAsync();
                    return ErrorInterno();
                }
                db.PerfilesAsesor.Add(new PerfilAsesor
                {
                    UsuarioId = user.Id,
                    TipoDocumento = tipo,
                    NumeroDocumento = numero,
                    NombreComercial = ValidacionAuth.NormalizarOpcional(request.NombreComercial),
                    EstadoAutorizacion = PerfilAsesor.Pendiente
                });
                Auditoria.Agregar(db, user.Id, user.Id, Auditoria.RegistroAsesor,
                    estadoAnterior: null, EstadosCuenta.Pendiente, motivo: null, incluirMotivo: false);
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync();
                db.ChangeTracker.Clear();
                // Registros simultáneos chocan en los índices únicos (correo o documento): 409, nunca 500.
                if (await users.FindByEmailAsync(email) is not null)
                    return Results.Conflict(new ErrorResponse(MensajeDuplicado));
                if (await DocumentoRegistrado(db, tipo, numero))
                    return DocumentoDuplicado();
                if (ex.InnerException is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry })
                    return Results.Conflict(new ErrorResponse(MensajeDuplicado));
                throw; // Lo atiende el manejador global: 500 con { error }.
            }

            log.LogInformation("Auditoria registro_asesor exitoso usuarioId={UsuarioId}", user.Id);
            return Results.Created("/api/auth/me", new RegistroCompradorResponse(user.Id, MensajeSolicitudAsesor));
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
            if (user is null)
            {
                log.LogWarning("Auditoria login fallido motivo=usuario_inexistente correo={Correo}",
                    ValidacionAuth.EnmascararEmail(email));
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

            // P2: primero la contraseña, después el estado; así el estado solo se revela a quien conoce la contraseña.
            if (user.EstadoCuenta != EstadosCuenta.Activa)
            {
                log.LogWarning("Auditoria login fallido motivo=cuenta_no_activa estado={Estado} usuarioId={UsuarioId}",
                    user.EstadoCuenta, user.Id);
                var mensaje = user.EstadoCuenta switch
                {
                    EstadosCuenta.Pendiente => MensajePendiente,
                    EstadosCuenta.Rechazada => MensajeRechazada,
                    EstadosCuenta.Bloqueada => MensajeBloqueada,
                    _ => null
                };
                // Un valor fuera de los estados del contrato no se revela: se responde como en P1.
                if (mensaje is null) return Credenciales();
                return Results.Json(new ErrorResponse(mensaje, Estado: user.EstadoCuenta),
                    statusCode: StatusCodes.Status403Forbidden);
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
            return Results.Ok(new MeResponse(user.Id, user.NombreCompleto, user.Email, user.PhoneNumber,
                userRoles.ToArray(), user.EstadoCuenta));
        }).RequireAuthorization(Roles.PoliticaAutenticado);

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
            return ErrorInterno();
        }).RequireAuthorization(Roles.PoliticaAutenticado);

        return app;
    }

    private static Task<bool> DocumentoRegistrado(CamionDbContext db, string tipo, string numero) =>
        db.PerfilesAsesor.AnyAsync(p => p.TipoDocumento == tipo && p.NumeroDocumento == numero);

    private static IResult DocumentoDuplicado() =>
        Results.Conflict(new ErrorResponse(MensajeDocumentoDuplicado,
            new Dictionary<string, string[]> { ["numeroDocumento"] = [MensajeDocumentoDuplicado] }));

    private static IResult ErrorInterno() =>
        Results.Json(new ErrorResponse(MensajesError.Interno), statusCode: StatusCodes.Status500InternalServerError);

    private static IResult Credenciales() =>
        Results.Json(new ErrorResponse(MensajeCredenciales), statusCode: StatusCodes.Status401Unauthorized);

    private static IResult Validacion(Dictionary<string, string[]> errores) =>
        Results.BadRequest(new ErrorResponse("Revisa los datos marcados.", errores));
}
