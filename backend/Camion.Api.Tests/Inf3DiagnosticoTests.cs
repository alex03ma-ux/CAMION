using Camion.Api.Data;
using Camion.Api.Tests.Infraestructura;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Camion.Api.Tests;

/// <summary>
/// INF-3: comprueba que el diagnóstico de la infraestructura de pruebas (<see cref="DiagnosticoTransaccionesSqlite"/>)
/// deja en el log el código de SQLite cuando BEGIN falla, sin ocultar la excepción. SQLite en memoria; nunca MariaDB.
/// </summary>
public sealed class Inf3DiagnosticoTests
{
    [Fact(DisplayName = "INF-3: si BEGIN falla en SQLite, la excepción sigue saliendo y el log guarda código, autocommit y mensaje")]
    public async Task Begin_fallido_queda_en_el_log()
    {
        await using var f = new CamionApiFactory();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CamionDbContext>();
        await db.Database.OpenConnectionAsync();
        // Una transacción abierta a mano (fuera de EF) provoca el error 1 de SQLite al intentar otra.
        await db.Database.ExecuteSqlRawAsync("BEGIN;");
        var e = await Assert.ThrowsAsync<SqliteException>(() => db.Database.BeginTransactionAsync());
        Assert.Equal(1, e.SqliteErrorCode);
        await db.Database.ExecuteSqlRawAsync("ROLLBACK;");

        var linea = Assert.Single(f.Logs, l => l.StartsWith(DiagnosticoTransaccionesSqlite.Categoria + "|Error|", StringComparison.Ordinal));
        Assert.Contains("código=1", linea, StringComparison.Ordinal);
        Assert.Contains("autocommit=0", linea, StringComparison.Ordinal);
        Assert.Contains("cannot start a transaction within a transaction", linea, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "INF-3: con el interceptor, una transacción normal se abre, se confirma y no deja nada en el log")]
    public async Task Transaccion_normal_sin_log()
    {
        await using var f = new CamionApiFactory();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CamionDbContext>();
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            Assert.Equal(0, await db.Users.CountAsync());
            await tx.CommitAsync();
        }
        Assert.DoesNotContain(f.Logs, l => l.StartsWith(DiagnosticoTransaccionesSqlite.Categoria, StringComparison.Ordinal));
    }
}
