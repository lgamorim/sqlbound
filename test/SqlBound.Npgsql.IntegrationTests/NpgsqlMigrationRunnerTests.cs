using global::Npgsql;
using SqlBound.Migrations;

namespace SqlBound.Npgsql.IntegrationTests;

/// <summary>End-to-end run and revert against PostgreSQL through <see cref="MigrationRunner"/>.</summary>
[Collection("PostgresMigrations")]
public sealed class NpgsqlMigrationRunnerTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Should_ApplyThenRevertMigration()
    {
        await using var connection = await ResetAsync("pg_widgets");
        var ledger = new NpgsqlMigrationLedger();
        var migrations = new[]
        {
            new Migration(20260712100000, "create_widgets",
                "CREATE TABLE pg_widgets (id integer PRIMARY KEY);", "DROP TABLE pg_widgets;", "aaa"),
        };

        var applied = await MigrationRunner.RunAsync(connection, ledger, migrations, TimeProvider.System, Token);

        Assert.Single(applied);
        Assert.True(await TableExistsAsync(connection, "pg_widgets"));

        var reverted = await MigrationRunner.RevertAsync(connection, ledger, migrations, Token);

        Assert.Equal(20260712100000, reverted!.Version);
        Assert.False(await TableExistsAsync(connection, "pg_widgets"));
        Assert.Empty(await ledger.GetAppliedAsync(connection, Token));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<NpgsqlConnection> ResetAsync(string table)
    {
        var connection = await fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS _sqlbound_migrations; DROP TABLE IF EXISTS {table};";
        await command.ExecuteNonQueryAsync(Token);
        return connection;
    }

    private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT to_regclass(@name) IS NOT NULL;";
        command.Parameters.AddWithValue("@name", table);
        return await command.ExecuteScalarAsync(Token) is true;
    }
}
