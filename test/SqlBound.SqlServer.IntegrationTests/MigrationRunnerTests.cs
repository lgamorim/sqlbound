using Microsoft.Data.SqlClient;
using SqlBound.Migrations;

namespace SqlBound.SqlServer.IntegrationTests;

/// <summary>
/// Exercises <see cref="MigrationRunner"/> end to end against the shared container with the real
/// SQL Server ledger. Each test drops the ledger and the tables its migrations create, so the
/// sequential methods start from a clean slate.
/// </summary>
public sealed class MigrationRunnerTests(SqlServerFixture fixture)
{
    private readonly SqlServerMigrationLedger _ledger = new();
    private readonly TimeProvider _clock = TimeProvider.System;

    [Fact]
    public async Task Should_ApplyAllPendingMigrations_When_Run()
    {
        await using var connection = await ResetAsync("mr_a", "mr_b");
        var migrations = new[]
        {
            Migration(20260712100000, "create_a", "CREATE TABLE dbo.mr_a (id int);", "aaa"),
            Migration(20260712110000, "create_b", "CREATE TABLE dbo.mr_b (id int);", "bbb"),
        };

        var applied = await MigrationRunner.RunAsync(connection, _ledger, migrations, _clock, Token);

        Assert.Equal([20260712100000, 20260712110000], applied.Select(migration => migration.Version));
        Assert.True(await TableExistsAsync(connection, "mr_a"));
        Assert.True(await TableExistsAsync(connection, "mr_b"));
        Assert.Equal(2, (await _ledger.GetAppliedAsync(connection, Token)).Count);
    }

    [Fact]
    public async Task Should_ApplyNothing_When_AlreadyUpToDate()
    {
        await using var connection = await ResetAsync("mr_a");
        var migrations = new[] { Migration(20260712100000, "create_a", "CREATE TABLE dbo.mr_a (id int);", "aaa") };
        await MigrationRunner.RunAsync(connection, _ledger, migrations, _clock, Token);

        var second = await MigrationRunner.RunAsync(connection, _ledger, migrations, _clock, Token);

        Assert.Empty(second);
        Assert.Single(await _ledger.GetAppliedAsync(connection, Token));
    }

    [Fact]
    public async Task Should_RollBackFailedMigrationAndStop_When_AScriptErrors()
    {
        await using var connection = await ResetAsync("mr_a", "mr_bad");
        var migrations = new[]
        {
            Migration(20260712100000, "create_a", "CREATE TABLE dbo.mr_a (id int);", "aaa"),
            Migration(20260712110000, "broken", "CREATE TABLE dbo.mr_bad (id int) THIS IS NOT SQL;", "bad"),
        };

        var exception = await Assert.ThrowsAsync<MigrationExecutionException>(
            () => MigrationRunner.RunAsync(connection, _ledger, migrations, _clock, Token));

        Assert.Equal(20260712110000, exception.Version);
        Assert.True(await TableExistsAsync(connection, "mr_a"));
        Assert.False(await TableExistsAsync(connection, "mr_bad"));
        Assert.Single(await _ledger.GetAppliedAsync(connection, Token));
    }

    [Fact]
    public async Task Should_ThrowInconsistency_When_AnAppliedMigrationHasBeenEdited()
    {
        await using var connection = await ResetAsync("mr_a");
        await MigrationRunner.RunAsync(
            connection, _ledger,
            [Migration(20260712100000, "create_a", "CREATE TABLE dbo.mr_a (id int);", "aaa")],
            _clock, Token);

        await Assert.ThrowsAsync<MigrationInconsistencyException>(
            () => MigrationRunner.RunAsync(
                connection, _ledger,
                [Migration(20260712100000, "create_a", "CREATE TABLE dbo.mr_a (id int, extra int);", "edited")],
                _clock, Token));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Migration Migration(long version, string name, string upScript, string checksum) =>
        // Real checksums are 64-char SHA-256 hex; pad so the CHAR(64) ledger column round-trips exactly.
        new(version, name, upScript, $"DROP TABLE dbo.{name};", checksum.PadRight(64, '0'));

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
