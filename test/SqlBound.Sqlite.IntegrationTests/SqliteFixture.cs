using Microsoft.Data.Sqlite;
using SqlBound.Sqlite.IntegrationTests;

[assembly: AssemblyFixture(typeof(SqliteFixture))]

namespace SqlBound.Sqlite.IntegrationTests;

/// <summary>
/// Seeds a named, shared-cache in-memory SQLite database once for the whole test assembly and
/// hands out a fresh connection per test - describing shares one <c>sqlite3*</c> handle's error
/// state (<c>sqlite3_errmsg</c>) across calls, so tests running in parallel on one connection can
/// observe each other's results. A shared-cache in-memory database (rather than the private
/// <c>:memory:</c> each connection would otherwise get) lets every connection see the same schema
/// without needing Docker or a temp file.
/// </summary>
public sealed class SqliteFixture : IAsyncLifetime
{
    private const string ConnectionString = "Data Source=sqlbound-tests;Mode=Memory;Cache=Shared";

    // Kept open for the fixture's lifetime: a shared-cache in-memory database is discarded once
    // its last connection closes.
    private readonly SqliteConnection _keepAlive = new(ConnectionString);

    public async ValueTask InitializeAsync()
    {
        await _keepAlive.OpenAsync();
        var command = _keepAlive.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.CommandText =
                """
                CREATE TABLE items (
                    id INTEGER NOT NULL PRIMARY KEY,
                    name TEXT NOT NULL,
                    price REAL NULL);

                CREATE TABLE every_type (
                    bool_col BOOLEAN NOT NULL,
                    tinyint_col TINYINT NOT NULL,
                    smallint_col SMALLINT NOT NULL,
                    int_col INT NOT NULL,
                    bigint_col BIGINT NOT NULL,
                    real_col REAL NOT NULL,
                    decimal_col DECIMAL(10,2) NOT NULL,
                    text_col TEXT NOT NULL,
                    blob_col BLOB NOT NULL,
                    guid_col GUID NOT NULL,
                    date_col DATE NOT NULL,
                    datetime_col DATETIME NULL,
                    unmapped_col SOMETYPE NULL);
                """;
            await command.ExecuteNonQueryAsync();
        }
    }

    public async ValueTask DisposeAsync() => await _keepAlive.DisposeAsync();

    public async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}
