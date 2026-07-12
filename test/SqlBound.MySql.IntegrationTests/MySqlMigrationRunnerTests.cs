using MySqlConnector;
using SqlBound.Migrations;

namespace SqlBound.MySql.IntegrationTests;

/// <summary>
/// End-to-end run and revert against MySQL through <see cref="MigrationRunner"/>. MySQL commits DDL
/// implicitly (ADR 0007), so the runner applies each migration without a transaction; this proves
/// that non-transactional path applies and reverts correctly.
/// </summary>
[Collection("MySqlMigrations")]
public sealed class MySqlMigrationRunnerTests(MySqlFixture fixture)
{
    [Fact]
    public async Task Should_ApplyThenRevertMigration()
    {
        await using var connection = await ResetAsync("my_widgets");
        var ledger = new MySqlMigrationLedger();
        var migrations = new[]
        {
            new Migration(20260712100000, "create_widgets",
                "CREATE TABLE my_widgets (id INT PRIMARY KEY);", "DROP TABLE my_widgets;", "aaa"),
        };

        var applied = await MigrationRunner.RunAsync(connection, ledger, migrations, TimeProvider.System, Token);

        Assert.Single(applied);
        Assert.True(await TableExistsAsync(connection, "my_widgets"));

        var reverted = await MigrationRunner.RevertAsync(connection, ledger, migrations, Token);

        Assert.Equal(20260712100000, reverted!.Version);
        Assert.False(await TableExistsAsync(connection, "my_widgets"));
        Assert.Empty(await ledger.GetAppliedAsync(connection, Token));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<MySqlConnection> ResetAsync(string table)
    {
        var connection = await fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP TABLE IF EXISTS _sqlbound_migrations; DROP TABLE IF EXISTS {table};";
        await command.ExecuteNonQueryAsync(Token);
        return connection;
    }

    private static async Task<bool> TableExistsAsync(MySqlConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = @name;";
        command.Parameters.AddWithValue("@name", table);
        return Convert.ToInt64(await command.ExecuteScalarAsync(Token)) > 0;
    }
}
