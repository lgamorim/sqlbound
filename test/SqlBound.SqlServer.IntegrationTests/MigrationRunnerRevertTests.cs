using Microsoft.Data.SqlClient;
using SqlBound.Migrations;

namespace SqlBound.SqlServer.IntegrationTests;

/// <summary>
/// Exercises <see cref="MigrationRunner.RevertAsync"/> end to end against the shared container.
/// Each test drops the ledger and its tables first so the sequential methods start clean.
/// </summary>
[Collection("SqlServerMigrations")]
public sealed class MigrationRunnerRevertTests(SqlServerFixture fixture)
{
    private readonly SqlServerMigrationLedger _ledger = new();
    private readonly TimeProvider _clock = TimeProvider.System;

    [Fact]
    public async Task Should_RunDownScriptAndRemoveLedgerRow_When_Reverting()
    {
        await using var connection = await ResetAsync("mrr_a", "mrr_b");
        var migrations = new[]
        {
            Migration(20260712100000, "create_a", "CREATE TABLE dbo.mrr_a (id int);"),
            Migration(20260712110000, "create_b", "CREATE TABLE dbo.mrr_b (id int);"),
        };
        await MigrationRunner.RunAsync(connection, _ledger, migrations, _clock, Token);

        var reverted = await MigrationRunner.RevertAsync(connection, _ledger, migrations, Token);

        Assert.Equal(20260712110000, reverted!.Version);
        Assert.True(await TableExistsAsync(connection, "mrr_a"));
        Assert.False(await TableExistsAsync(connection, "mrr_b"));
        Assert.Equal([20260712100000], (await _ledger.GetAppliedAsync(connection, Token)).Select(row => row.Version));
    }

    [Fact]
    public async Task Should_ReturnNull_When_NothingApplied()
    {
        await using var connection = await ResetAsync("mrr_a");
        var migrations = new[] { Migration(20260712100000, "create_a", "CREATE TABLE dbo.mrr_a (id int);") };
        await _ledger.EnsureCreatedAsync(connection, Token);

        Assert.Null(await MigrationRunner.RevertAsync(connection, _ledger, migrations, Token));
    }

    [Fact]
    public async Task Should_ThrowInconsistency_When_TargetMigrationIsIrreversible()
    {
        await using var connection = await ResetAsync("mrr_a");
        var irreversible = new Migration(
            20260712100000, "create_a", "CREATE TABLE dbo.mrr_a (id int);", DownScript: null, Checksum("a"));
        await MigrationRunner.RunAsync(connection, _ledger, [irreversible], _clock, Token);

        await Assert.ThrowsAsync<MigrationInconsistencyException>(
            () => MigrationRunner.RevertAsync(connection, _ledger, [irreversible], Token));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Checksum(string seed) => seed.PadRight(64, '0');

    private static Migration Migration(long version, string name, string upScript) =>
        new(version, name, upScript, $"DROP TABLE dbo.{name.Replace("create_", "mrr_")};", Checksum(name));

    private async Task<SqlConnection> ResetAsync(params string[] tables)
    {
        var connection = await fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        var drops = string.Join("\n", tables.Select(table => $"DROP TABLE IF EXISTS dbo.{table};"));
        command.CommandText = $"DROP TABLE IF EXISTS dbo._sqlbound_migrations;\n{drops}";
        await command.ExecuteNonQueryAsync(Token);
        return connection;
    }

    private static async Task<bool> TableExistsAsync(SqlConnection connection, string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT OBJECT_ID(@name, N'U');";
        command.Parameters.AddWithValue("@name", $"dbo.{table}");
        return await command.ExecuteScalarAsync(Token) is not (null or DBNull);
    }
}
