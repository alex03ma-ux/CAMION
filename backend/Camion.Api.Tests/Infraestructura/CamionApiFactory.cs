using System.Collections.Concurrent;
using System.Security.Cryptography;
using Camion.Api.Data;
using Camion.Api.Media;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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
    // INF-3: Pooling=False. El pool de Microsoft.Data.Sqlite 8.0.x tiene una carrera: SqliteConnectionInternal.Activate
    // marca la conexión activa antes de enlazarla a su SqliteConnection, y Deactivate la desenlaza antes de marcarla
    // inactiva. Si en ese instante otra apertura encuentra el pool vacío, ReclaimLeakedConnections la toma por "perdida"
    // y la devuelve al pool, y la misma conexión nativa acaba en dos peticiones a la vez. Con peticiones simultáneas
    // (Task.WhenAll) daba un 500 de vez en cuando: BEGIN dentro de la transacción de la otra petición o
    // sqlite3_create_function con sentencias activas en Open() (PagosTests.Anular_concurrente y
    // RegistroAsesorTests.Simultaneos_*). Sin pool cada apertura crea su propia conexión nativa contra la misma base en
    // memoria (la mantiene viva _conexionViva). Producción usa MySqlConnector, no este pool.
    private readonly string _cadenaSqlite =
        $"Data Source=file:camion_pruebas_{Guid.NewGuid():N}?mode=memory&cache=shared;Default Timeout=30;Pooling=False";
    private readonly SqliteConnection _conexionViva;

    public ConcurrentQueue<string> Logs { get; } = new();

    /// <summary>
    /// P4a (D-20): carpeta temporal única para las fotos (Media:Ruta). Nunca C:\CAMION_media. Se borra en Dispose,
    /// junto con <see cref="CarpetaFueraDeMedia"/> (carpeta hermana para probar rutas que salen de la raíz).
    /// </summary>
    public string CarpetaMedia { get; } = Path.Combine(Path.GetTempPath(), $"camion_media_pruebas_{Guid.NewGuid():N}");
    public string CarpetaFueraDeMedia => CarpetaMedia + "_fuera";

    /// <summary>D-78 (M-2): base ficticia del enlace de recuperación en las pruebas.</summary>
    public const string UrlPortalPruebas = "https://portal.prueba.test";

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
        builder.UseSetting("Media:Ruta", CarpetaMedia);
        // P5 (CA-P5-20, CA-P5-21): límites del contacto público muy altos para que las pruebas no se vuelvan frágiles
        // (todas llegan sin IP). ContactosLimitesTests los baja con su propia configuración.
        builder.UseSetting("Contactos:LimiteVentanaCorta", "100000");
        builder.UseSetting("Contactos:LimiteDiario", "100000");
        builder.UseSetting("Contactos:LimiteTelefonoDia", "100000");
        // P8 (D-73 D): mismo criterio para el cupo de restablecer la contraseña; P8RecuperacionTests lo baja.
        builder.UseSetting("Recuperacion:LimiteVentana", "100000");
        // D-78: el entorno Testing no es Development, así que el enlace debe ser https://; las claves de DataProtection
        // irían a una carpeta temporal (además se sustituye el proveedor por uno efímero, abajo).
        builder.UseSetting("Recuperacion:UrlPortal", UrlPortalPruebas);
        builder.UseSetting("DataProtection:RutaClaves", CarpetaMedia + "_claves");
        // Además de la configuración, se sustituye el almacén por uno con la carpeta temporal (después de Program).
        // P8: claves de DataProtection solo en memoria (no se escriben en el perfil del usuario).
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IAlmacenFotos>(new AlmacenFotosLocal(CarpetaMedia));
            services.AddSingleton<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(
                new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CamionDbContext>));
            if (descriptor is not null) services.Remove(descriptor);
            // INF-3: el interceptor solo añade diagnóstico si BEGIN falla (código SQLite, mensaje y estado de la conexión).
            services.AddDbContext<CamionDbContext>((sp, options) => options.UseSqlite(_cadenaSqlite)
                .AddInterceptors(new DiagnosticoTransaccionesSqlite(
                    sp.GetRequiredService<ILoggerFactory>().CreateLogger(DiagnosticoTransaccionesSqlite.Categoria))));

            services.AddLogging(logging => logging.AddProvider(new ProveedorLogPrueba(Logs)));

            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            scope.ServiceProvider.GetRequiredService<CamionDbContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        _conexionViva.Dispose();
        foreach (var carpeta in new[] { CarpetaMedia, CarpetaFueraDeMedia, CarpetaMedia + "_claves" })
        {
            try
            {
                if (Directory.Exists(carpeta)) Directory.Delete(carpeta, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
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
