using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Camion.Api.Tests.Infraestructura;

/// <summary>
/// INF-3 (D-1 de P7): <c>PagosTests.Anular_concurrente</c> falló con un 500 porque el <c>BEGIN IMMEDIATE</c> de SQLite lanzó
/// una <see cref="SqliteException"/> que no era de bloqueo (esas las reintenta Microsoft.Data.Sqlite durante 30 s). La causa
/// era el pool de Microsoft.Data.Sqlite, que con mucha concurrencia podía entregar la misma conexión nativa a dos peticiones;
/// se corrigió con <c>Pooling=False</c> en <see cref="CamionApiFactory"/>. El manejador global del API no registra el
/// mensaje (a propósito), así que este interceptor queda como guarda para cualquier fallo futuro al abrir una transacción.
/// Abre la transacción exactamente igual que EF Core (<c>DbConnection.BeginTransaction(nivel)</c>) y, si
/// falla, deja en el log de las pruebas (consola, y por tanto el .trx, y <see cref="CamionApiFactory.Logs"/>) el código,
/// el código extendido, el mensaje y si la conexión ya estaba dentro de una transacción. No reintenta ni oculta nada:
/// la excepción se vuelve a lanzar y la prueba falla igual que antes.
/// </summary>
internal sealed class DiagnosticoTransaccionesSqlite(ILogger log) : DbTransactionInterceptor
{
    public const string Categoria = "Camion.Pruebas.SqliteTransacciones";

    public override InterceptionResult<DbTransaction> TransactionStarting(
        DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result)
    {
        if (result.HasResult) return result;
        try
        {
            return InterceptionResult<DbTransaction>.SuppressWithResult(connection.BeginTransaction(eventData.IsolationLevel));
        }
        catch (SqliteException e)
        {
            Registrar(connection, e);
            throw;
        }
    }

    public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
        DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result,
        CancellationToken cancellationToken = default)
    {
        if (result.HasResult) return result;
        try
        {
            return InterceptionResult<DbTransaction>.SuppressWithResult(
                await connection.BeginTransactionAsync(eventData.IsolationLevel, cancellationToken));
        }
        catch (SqliteException e)
        {
            Registrar(connection, e);
            throw;
        }
    }

    private void Registrar(DbConnection connection, SqliteException e)
    {
        // sqlite3_get_autocommit = 0 significa que la conexión (quizá reutilizada del pool) ya tenía una transacción abierta.
        string autocommit;
        try
        {
            autocommit = connection is SqliteConnection { Handle: { } h }
                ? SQLitePCL.raw.sqlite3_get_autocommit(h).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "?";
        }
        catch (Exception)
        {
            autocommit = "?";
        }
        log.LogError(
            "INF-3: BEGIN falló en SQLite: código={Codigo} extendido={Extendido} autocommit={Autocommit} mensaje={Mensaje}",
            e.SqliteErrorCode, e.SqliteExtendedErrorCode, autocommit, e.Message);
    }
}
