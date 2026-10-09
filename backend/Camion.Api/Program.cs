using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
var app = builder.Build();

// La API responde aunque la base de datos este apagada.
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "Camion.Api" }));

// Comprobacion explicita de conexion a MariaDB, sin exponer credenciales.
app.MapGet("/health/database", async (IConfiguration configuration, CancellationToken cancellationToken) =>
{
    var connectionString = configuration.GetConnectionString("CamionDatabase");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.Problem("Configura ConnectionStrings:CamionDatabase en secretos locales.", statusCode: 503);
    }

    try
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_type = 'BASE TABLE'",
            connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Results.Ok(new
        {
            status = "ok",
            database = connection.Database,
            tables = Convert.ToInt32(result)
        });
    }
    catch (MySqlException)
    {
        return Results.Problem("No fue posible conectar con MariaDB. Revisa XAMPP, puerto, usuario y contrasena.", statusCode: 503);
    }
});

app.Run();