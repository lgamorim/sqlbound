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
    public async Task Should_RecordAppliedMigrationsAndReadThemBackOrderedByVersion()
    {
        await using var connection = await ResetAsync();
        var ledger = new SqlServerMigrationLedger();
        await ledger.EnsureCreatedAsync(connection, TestContext.Current.CancellationToken);
        var second = new AppliedMigration(20260712150000, "second", SampleChecksum, new DateTime(2026, 7, 12, 15, 0, 0), 12);
        var first = new AppliedMigration(20260712143000, "first", SampleChecksum, new DateTime(2026, 7, 12, 14, 30, 0), 7);
        await ledger.RecordAppliedAsync(connection, null, second, TestContext.Current.CancellationToken);
        await ledger.RecordAppliedAsync(connection, null, first, TestContext.Current.CancellationToken);

        var applied = await ledger.GetAppliedAsync(connection, TestContext.Current.CancellationToken);

        Assert.Equal([first, second], applied);
    }

    [Fact]
    public async Task Should_RemoveOnlyTheNamedMigration_When_RemoveCalled()
    {
        await using var connection = await ResetAsync();
        var ledger = new SqlServerMigrationLedger();
        await ledger.EnsureCreatedAsync(connection, TestContext.Current.CancellationToken);
        var first = new AppliedMigration(20260712143000, "first", SampleChecksum, new DateTime(2026, 7, 12, 14, 30, 0), 7);
        var second = new AppliedMigration(20260712150000, "second", SampleChecksum, new DateTime(2026, 7, 12, 15, 0, 0), 12);
        await ledger.RecordAppliedAsync(connection, null, first, TestContext.Current.CancellationToken);
        await ledger.RecordAppliedAsync(connection, null, second, TestContext.Current.CancellationToken);

        await ledger.RemoveAsync(connection, null, second.Version, TestContext.Current.CancellationToken);

        Assert.Equal([first], await ledger.GetAppliedAsync(connection, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_LeaveNoRow_When_TransactionRecordingAMigrationIsRolledBack()
    {
        await using var connection = await ResetAsync();
        var ledger = new SqlServerMigrationLedger();
        await ledger.EnsureCreatedAsync(connection, TestContext.Current.CancellationToken);
        var migration = new AppliedMigration(20260712143000, "first", SampleChecksum, new DateTime(2026, 7, 12, 14, 30, 0), 7);

        await using (var transaction = (SqlTransaction)await connection.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await ledger.RecordAppliedAsync(connection, transaction, migration, TestContext.Current.CancellationToken);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        Assert.Empty(await ledger.GetAppliedAsync(connection, TestContext.Current.CancellationToken));
    }

    private async Task<SqlConnection> ResetAsync()
    {
        var connection = await fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE IF EXISTS dbo._sqlbound_migrations;";
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return connection;
    }
}
