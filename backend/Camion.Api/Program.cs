using System.Security.Claims;
using System.Text;
using Camion.Api.Auth;
using Camion.Api.Comandos;
using Camion.Api.Contracts;
using Camion.Api.Data;
using Camion.Api.Endpoints;
using Camion.Api.Media;
using Camion.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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
// RN-SE-006: políticas con nombre; el 403 se escribe con el formato común { error }.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Roles.PoliticaSoloAdministrador, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Administrador));
    options.AddPolicy(Roles.PoliticaSoloAsesor, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Asesor));
    options.AddPolicy(Roles.PoliticaAutenticado, policy => policy.RequireAuthenticatedUser());
    options.AddPolicy(Roles.PoliticaAsesorOAdministrador,
        policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Asesor, Roles.Administrador));
});
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, RespuestaAutorizacion>();
// P3: hora inyectable para la regla de vigencia del catálogo (las pruebas la fijan).
builder.Services.AddSingleton(TimeProvider.System);
// P4a (D-20): fotos en una carpeta fuera del repositorio (Media:Ruta; por defecto C:\CAMION_media).
// Las pruebas sustituyen IAlmacenFotos por una carpeta temporal.
builder.Services.AddSingleton<IAlmacenFotos>(sp =>
    new AlmacenFotosLocal(sp.GetRequiredService<IConfiguration>()["Media:Ruta"]));
// JSON malformado o con tipos incorrectos: el framework lanza BadHttpRequestException y se responde con el formato común.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
var app = builder.Build();

// D-13 (opción A): `dotnet run --project backend/Camion.Api -- promover-admin <correo>` no arranca el servidor web.
if (args.Length > 0 && args[0] == PromoverAdmin.NombreComando)
{
    using var scope = app.Services.CreateScope();
    var resultado = await PromoverAdmin.EjecutarAsync(
        args.Length > 1 ? args[1] : null,
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
        scope.ServiceProvider.GetRequiredService<CamionDbContext>());
    (resultado.CodigoSalida == 0 ? Console.Out : Console.Error).WriteLine(resultado.Mensaje);
    Environment.ExitCode = resultado.CodigoSalida;
    return;
}

// RN-SE-006: los tres roles existen desde el arranque. Si la base no responde, la API arranca igual
// (los registros vuelven a asegurar su rol) y /health/database informa del problema.
using (var scope = app.Services.CreateScope())
{
    try
    {
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = scope.ServiceProvider.GetRequiredService<CamionDbContext>();
        foreach (var rol in Roles.Todos)
            await Roles.AsegurarAsync(roles, db, rol);
    }
    catch (Exception ex)
    {
        // Sin el mensaje de la excepción (podría incluir datos de conexión).
        app.Logger.LogWarning("No se pudieron asegurar los roles al iniciar: {Tipo}", ex.GetType().FullName);
    }
}

app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (BadHttpRequestException ex) when (!context.Response.HasStarted)
    {
        context.Response.Clear();
        context.Response.StatusCode = ex.StatusCode;
        await context.Response.WriteAsJsonAsync(new ErrorResponse(MensajesError.FormatoInvalido));
    }
    catch (Exception ex) when (!context.RequestAborted.IsCancellationRequested)
    {
        // Sin el mensaje de la excepción: puede contener datos personales (p. ej. el valor de un índice único).
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Camion.Api.Errores").LogError(
            "Error no controlado {Tipo} (interno: {TipoInterno}) en {Metodo} {Ruta} traceId={TraceId} pila={Pila}",
            ex.GetType().FullName, ex.InnerException?.GetType().FullName, context.Request.Method,
            context.Request.Path.Value, context.TraceIdentifier, ex.StackTrace);
        if (context.Response.HasStarted) throw;
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new ErrorResponse(MensajesError.Interno));
    }
});

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
        return Results.Json(new ErrorResponse("No fue posible conectar con MariaDB."), statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapAuthEndpoints(jwtKey);
app.MapAdminUsuariosEndpoints();
app.MapCatalogoEndpoints();
app.MapInventarioEndpoints();
app.MapMediaEndpoints();
// P4p: planes, suscripciones y catálogos administrables (D-26, D-37).
app.MapPlanesEndpoints();
app.MapSuscripcionesEndpoints();
app.MapAdminCatalogosEndpoints();

app.Run();

// Permite pruebas de integración con WebApplicationFactory<Program>.
public partial class Program { }
