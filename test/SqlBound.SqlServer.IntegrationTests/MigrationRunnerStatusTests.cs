using Microsoft.Data.SqlClient;
using SqlBound.Migrations;

namespace SqlBound.SqlServer.IntegrationTests;

/// <summary>
/// Exercises <see cref="MigrationRunner.StatusAsync"/> end to end against the shared container:
/// the pure classification is unit-tested, so this proves the read-only wiring over the real ledger.
/// </summary>
[Collection("SqlServerMigrations")]
public sealed class MigrationRunnerStatusTests(SqlServerFixture fixture)
{
    private readonly SqlServerMigrationLedger _ledger = new();

    [Fact]
    public async Task Should_ReportAppliedAndPending_When_SomeMigrationsApplied()
    {
        await using var connection = await ResetAsync("ms_a");
        var first = new Migration(20260712100000, "create_a", "CREATE TABLE dbo.ms_a (id int);", "DROP TABLE dbo.ms_a;", Checksum("a"));
        await MigrationRunner.RunAsync(connection, _ledger, [first], TimeProvider.System, Token);
        var second = new Migration(20260712110000, "create_b", "SELECT 1;", "SELECT 1;", Checksum("b"));

        var report = await MigrationRunner.StatusAsync(connection, _ledger, [first, second], Token);

        Assert.Equal(MigrationState.Applied, report[0].State);
        Assert.NotNull(report[0].AppliedOnUtc);
        Assert.Equal(MigrationState.Pending, report[1].State);
    }

    [Fact]
    public async Task Should_ReportEmpty_When_NoMigrationsOnDiskOrInLedger()
    {
        await using var connection = await ResetAsync();

        Assert.Empty(await MigrationRunner.StatusAsync(connection, _ledger, [], Token));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Checksum(string seed) => seed.PadRight(64, '0');

    private async Task<SqlConnection> ResetAsync(params string[] tables)
    {
        var connection = await fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        var drops = string.Join("\n", tables.Select(table => $"DROP TABLE IF EXISTS dbo.{table};"));
        command.CommandText = $"DROP TABLE IF EXISTS dbo._sqlbound_migrations;\n{drops}";
        await command.ExecuteNonQueryAsync(Token);
        return connection;
    }
}
