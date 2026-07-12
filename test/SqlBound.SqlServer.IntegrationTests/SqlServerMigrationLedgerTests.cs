using Microsoft.Data.SqlClient;
using SqlBound.Migrations;

namespace SqlBound.SqlServer.IntegrationTests;

/// <summary>
/// Exercises <see cref="SqlServerMigrationLedger"/> against the shared container. The methods run
/// sequentially (one xunit collection) and each drops the ledger table first, so they do not
/// observe one another's rows; the describe suites run in parallel but only read other tables.
/// </summary>
public sealed class SqlServerMigrationLedgerTests(SqlServerFixture fixture)
{
    private const string SampleChecksum =
        "0000000000000000000000000000000000000000000000000000000000000000";

    [Fact]
    public async Task Should_CreateLedgerAndReadEmpty_When_NoMigrationsApplied()
    {
        await using var connection = await ResetAsync();
        var ledger = new SqlServerMigrationLedger();

        await ledger.EnsureCreatedAsync(connection, TestContext.Current.CancellationToken);

        Assert.Empty(await ledger.GetAppliedAsync(connection, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_NotThrow_When_EnsureCreatedCalledTwice()
    {
        await using var connection = await ResetAsync();
        var ledger = new SqlServerMigrationLedger();

        await ledger.EnsureCreatedAsync(connection, TestContext.Current.CancellationToken);
        await ledger.EnsureCreatedAsync(connection, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Should_ReturnAppliedMigrationsOrderedByVersion_When_LedgerHasRows()
    {
        await using var connection = await ResetAsync();
        var ledger = new SqlServerMigrationLedger();
        await ledger.EnsureCreatedAsync(connection, TestContext.Current.CancellationToken);
        await SeedAsync(connection, 20260712150000, "second", 12);
        await SeedAsync(connection, 20260712143000, "first", 7);

        var applied = await ledger.GetAppliedAsync(connection, TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new AppliedMigration(20260712143000, "first", SampleChecksum, new DateTime(2026, 7, 12, 14, 30, 0), 7),
                new AppliedMigration(20260712150000, "second", SampleChecksum, new DateTime(2026, 7, 12, 15, 0, 0), 12),
            ],
            applied);
    }

    private async Task<SqlConnection> ResetAsync()
    {
        var connection = await fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE IF EXISTS dbo._sqlbound_migrations;";
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static async Task SeedAsync(SqlConnection connection, long version, string name, long executionMs)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO dbo._sqlbound_migrations (version, name, checksum, applied_on_utc, execution_ms)
            VALUES (@version, @name, @checksum,
                CASE @version WHEN 20260712143000 THEN '2026-07-12T14:30:00' ELSE '2026-07-12T15:00:00' END,
                @executionMs);
            """;
        command.Parameters.AddWithValue("@version", version);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@checksum", SampleChecksum);
        command.Parameters.AddWithValue("@executionMs", executionMs);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
