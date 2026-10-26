using System.Collections.Concurrent;
using System.Security.Cryptography;
using Camion.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Camion.Api.Tests.Infraestructura;

/// <summary>
/// Levanta Camion.Api en memoria con SQLite en memoria. Nunca usa MariaDB ni la base camion_ventas:
/// la cadena de conexión es ficticia (puerto 1, base inexistente) y el DbContext se reemplaza por SQLite.
/// El entorno es "Testing" para que no se carguen los user-secrets de desarrollo.
/// </summary>
public sealed class CamionApiFactory : WebApplicationFactory<Program>
{
    // Clave de firma de prueba generada en cada ejecución (48 bytes aleatorios en Base64 = 64 caracteres; nunca un secreto real).
    public string JwtKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    // Base SQLite en memoria con caché compartida: la conexión que se mantiene abierta conserva la base
    // viva; cada DbContext abre la suya contra el mismo nombre, lo que permite peticiones en paralelo.
    private readonly string _cadenaSqlite =
        $"Data Source=file:camion_pruebas_{Guid.NewGuid():N}?mode=memory&cache=shared;Default Timeout=30";
    private readonly SqliteConnection _conexionViva;

    public ConcurrentQueue<string> Logs { get; } = new();

    public CamionApiFactory()
    {
        _conexionViva = new SqliteConnection(_cadenaSqlite);
        _conexionViva.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:CamionDatabase",
            "Server=127.0.0.1;Port=1;Database=prueba_inexistente;User=prueba_ficticia");
        builder.UseSetting("Jwt:Key", JwtKey);

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CamionDbContext>));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddDbContext<CamionDbContext>(options => options.UseSqlite(_cadenaSqlite));

            services.AddLogging(logging => logging.AddProvider(new ProveedorLogPrueba(Logs)));

            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            scope.ServiceProvider.GetRequiredService<CamionDbContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _conexionViva.Dispose();
    }
}

internal sealed class ProveedorLogPrueba(ConcurrentQueue<string> destino) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new LogPrueba(categoryName, destino);
    public void Dispose() { }

    private sealed class LogPrueba(string categoria, ConcurrentQueue<string> destino) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            destino.Enqueue($"{categoria}|{logLevel}|{formatter(state, exception)}|{exception}");
    }
}
