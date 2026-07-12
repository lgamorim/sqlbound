using MySqlConnector;
using SqlBound.Migrations;

namespace SqlBound.MySql.IntegrationTests;

/// <summary>
/// Exercises <see cref="MySqlMigrationLedger"/> against the shared container. Ledger and runner
/// tests share the <c>_sqlbound_migrations</c> table, so they run sequentially in one collection;
/// each drops the table first.
/// </summary>
[Collection("MySqlMigrations")]
public sealed class MySqlMigrationLedgerTests(MySqlFixture fixture)
{
    [Fact]
    public async Task Should_CreateLedgerAndReadEmpty_When_NoMigrationsApplied()
    {
        await using var connection = await ResetAsync();
        var ledger = new MySqlMigrationLedger();

        await ledger.EnsureCreatedAsync(connection, Token);

        Assert.Empty(await ledger.GetAppliedAsync(connection, Token));
    }

    [Fact]
    public async Task Should_RecordAppliedMigrationsAndReadThemBackOrderedByVersion()
    {
        await using var connection = await ResetAsync();
        var ledger = new MySqlMigrationLedger();
        await ledger.EnsureCreatedAsync(connection, Token);
        var second = new AppliedMigration(20260712150000, "second", "bbb", new DateTime(2026, 7, 12, 15, 0, 0), 12);
        var first = new AppliedMigration(20260712143000, "first", "aaa", new DateTime(2026, 7, 12, 14, 30, 0), 7);
        await ledger.RecordAppliedAsync(connection, null, second, Token);
        await ledger.RecordAppliedAsync(connection, null, first, Token);

        Assert.Equal([first, second], await ledger.GetAppliedAsync(connection, Token));
    }

    [Fact]
    public async Task Should_RemoveOnlyTheNamedMigration_When_RemoveCalled()
    {
        await using var connection = await ResetAsync();
        var ledger = new MySqlMigrationLedger();
        await ledger.EnsureCreatedAsync(connection, Token);
        var first = new AppliedMigration(20260712143000, "first", "aaa", new DateTime(2026, 7, 12, 14, 30, 0), 7);
        var second = new AppliedMigration(20260712150000, "second", "bbb", new DateTime(2026, 7, 12, 15, 0, 0), 12);
        await ledger.RecordAppliedAsync(connection, null, first, Token);
        await ledger.RecordAppliedAsync(connection, null, second, Token);

        await ledger.RemoveAsync(connection, null, second.Version, Token);

        Assert.Equal([first], await ledger.GetAppliedAsync(connection, Token));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<MySqlConnection> ResetAsync()
    {
        var connection = await fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE IF EXISTS _sqlbound_migrations;";
        await command.ExecuteNonQueryAsync(Token);
        return connection;
    }
}
