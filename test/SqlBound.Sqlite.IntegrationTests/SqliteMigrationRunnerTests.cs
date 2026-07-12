using Microsoft.Data.Sqlite;
using SqlBound.Migrations;

namespace SqlBound.Sqlite.IntegrationTests;

/// <summary>
/// End-to-end run and revert against SQLite through <see cref="MigrationRunner"/>, proving the
/// provider-neutral engine drives the SQLite ledger and transactional DDL correctly.
/// </summary>
public sealed class SqliteMigrationRunnerTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"sqlbound-runner-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    [Fact]
    public async Task Should_ApplyThenRevertMigration()
    {
        await using var connection = await OpenAsync();
        var ledger = new SqliteMigrationLedger();
        var migrations = new[]
        {
            new Migration(20260712100000, "create_widgets",
                "CREATE TABLE widgets (id INTEGER PRIMARY KEY);", "DROP TABLE widgets;", "aaa"),
        };

        var applied = await MigrationRunner.RunAsync(connection, ledger, migrations, TimeProvider.System, Token);

        Assert.Single(applied);
        Assert.True(await TableExistsAsync(connection, "widgets"));

        var reverted = await MigrationRunner.RevertAsync(connection, ledger, migrations, Token);

        Assert.Equal(20260712100000, reverted!.Version);
        Assert.False(await TableExistsAsync(connection, "widgets"));
        Assert.Empty(await ledger.GetAppliedAsync(connection, Token));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _path }.ConnectionString);
        await connection.OpenAsync(Token);
        return connection;
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = @name;";
        command.Parameters.AddWithValue("@name", table);
        return Convert.ToInt64(await command.ExecuteScalarAsync(Token)) > 0;
    }
}
