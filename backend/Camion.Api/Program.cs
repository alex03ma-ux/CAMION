using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("CamionDatabase")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:CamionDatabase en secretos locales.");
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Falta Jwt:Key en secretos locales.");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key debe tener por lo menos 32 bytes.");

builder.Services.AddDbContext<CamionDbContext>(options =>
    options.UseMySql(connectionString, new MariaDbServerVersion(new Version(10, 4, 32))));
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<CamionDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "Camion.Api",
            ValidateAudience = true,
            ValidAudience = "Camion.Frontend",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();
var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "Camion.Api" }));
app.MapGet("/health/database", async (IConfiguration configuration, CancellationToken cancellationToken) =>
{
    try
    {
        await using var connection = new MySqlConnection(configuration.GetConnectionString("CamionDatabase"));
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE'",
            connection);
        var count = await command.ExecuteScalarAsync(cancellationToken);
        return Results.Ok(new { status = "ok", database = connection.Database, tables = Convert.ToInt32(count) });
    }
    catch (MySqlException)
    {
        return Results.Problem("No fue posible conectar con MariaDB.", statusCode: 503);
    }
});

app.MapPost("/api/auth/registro/comprador", async (
    RegistroCompradorRequest request,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole> roles,
    CamionDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.NombreCompleto) ||
        string.IsNullOrWhiteSpace(request.Telefono) ||
        string.IsNullOrWhiteSpace(request.Email) ||
        string.IsNullOrWhiteSpace(request.Password) ||
        !new EmailAddressAttribute().IsValid(request.Email) ||
        request.NombreCompleto.Trim().Length is < 2 or > 150 ||
        request.Telefono.Trim().Length > 25 ||
        request.Password.Length < 8)
        return Results.BadRequest(new { error = "Verifica nombre, correo, telefono y contrasena." });

    var email = request.Email.Trim().ToLowerInvariant();
    if (await users.FindByEmailAsync(email) is not null)
        return Results.Conflict(new { error = "Este correo ya esta registrado." });

    // El rol se crea una sola vez si aun no existe.
    if (!await roles.RoleExistsAsync("COMPRADOR"))
    {
        var roleResult = await roles.CreateAsync(new IdentityRole("COMPRADOR"));
        if (!roleResult.Succeeded && !await roles.RoleExistsAsync("COMPRADOR"))
            return Results.Problem("No se pudo preparar el rol comprador.");
    }

    await using var tx = await db.Database.BeginTransactionAsync();
    var user = new ApplicationUser
    {
        Id = Guid.NewGuid().ToString(),
        UserName = email,
        Email = email,
        PhoneNumber = request.Telefono.Trim(),
        NombreCompleto = request.NombreCompleto.Trim()
    };
    var created = await users.CreateAsync(user, request.Password);
    if (!created.Succeeded)
    {
        await tx.RollbackAsync();
        return Results.BadRequest(new { errors = created.Errors.Select(e => e.Description) });
    }
    var assigned = await users.AddToRoleAsync(user, "COMPRADOR");
    if (!assigned.Succeeded)
    {
        await tx.RollbackAsync();
        return Results.Problem("No fue posible asignar el rol comprador.");
    }
    db.PerfilesComprador.Add(new PerfilComprador { UsuarioId = user.Id });
    await db.SaveChangesAsync();
    await tx.CommitAsync();
    return Results.Created($"/api/auth/me", new { usuarioId = user.Id, mensaje = "Comprador registrado correctamente. Ya puedes iniciar sesion." });
});

app.MapPost("/api/auth/login", async (
    LoginRequest request,
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    CamionDbContext db) =>
{
    var user = await users.FindByEmailAsync(request.Email?.Trim() ?? "");
    if (user is null || user.EstadoCuenta != "ACTIVA")
        return Results.Unauthorized();
    var check = await signIn.CheckPasswordSignInAsync(user, request.Password ?? "", lockoutOnFailure: true);
    if (!check.Succeeded) return Results.Unauthorized();
    var userRoles = await users.GetRolesAsync(user);
    var expiry = DateTime.UtcNow.AddHours(2);
    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, user.Id),
        new(ClaimTypes.NameIdentifier, user.Id),
        new(ClaimTypes.Name, user.NombreCompleto),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new("session_version", user.SessionVersion.ToString())
    };
    claims.AddRange(userRoles.Select(role => new Claim(ClaimTypes.Role, role)));
    var token = new JwtSecurityToken(
        issuer: "Camion.Api",
        audience: "Camion.Frontend",
        claims: claims,
        expires: expiry,
        signingCredentials: new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), SecurityAlgorithms.HmacSha256));
    user.UltimoAccesoEn = DateTime.UtcNow;
    await users.UpdateAsync(user);
    return Results.Ok(new AuthResponse(
        new JwtSecurityTokenHandler().WriteToken(token), "Bearer", expiry,
        user.Id, user.NombreCompleto, user.Email ?? "", userRoles.ToArray()));
});

app.MapGet("/api/auth/me", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users) =>
{
    var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (id is null) return Results.Unauthorized();
    var user = await users.FindByIdAsync(id);
    if (user is null || user.EstadoCuenta != "ACTIVA") return Results.Unauthorized();
    if (!int.TryParse(principal.FindFirstValue("session_version"), out var version) ||
        version != user.SessionVersion) return Results.Unauthorized();
    var userRoles = await users.GetRolesAsync(user);
    return Results.Ok(new { usuarioId = user.Id, user.NombreCompleto, user.Email, user.PhoneNumber, roles = userRoles });
}).RequireAuthorization();

app.Run();
