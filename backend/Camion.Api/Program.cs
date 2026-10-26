using System.Security.Claims;
using System.Text;
using Camion.Api.Data;
using Camion.Api.Endpoints;
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
            ValidIssuer = AuthEndpoints.Emisor,
            ValidateAudience = true,
            ValidAudience = AuthEndpoints.Audiencia,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        // RN-SE-006: en todos los endpoints protegidos la cuenta debe estar ACTIVA y la sesión vigente.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var user = id is null ? null : await users.FindByIdAsync(id);
                if (user is null || user.EstadoCuenta != "ACTIVA" ||
                    !int.TryParse(context.Principal!.FindFirstValue(AuthEndpoints.ClaimSessionVersion), out var version) ||
                    version != user.SessionVersion)
                    context.Fail("Sesión no vigente.");
            }
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

app.MapAuthEndpoints(jwtKey);

app.Run();

// Permite pruebas de integración con WebApplicationFactory<Program>.
public partial class Program { }
