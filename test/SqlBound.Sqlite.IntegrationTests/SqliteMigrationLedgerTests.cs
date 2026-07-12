using Microsoft.Data.Sqlite;
using SqlBound.Migrations;

namespace SqlBound.Sqlite.IntegrationTests;

/// <summary>
/// Exercises <see cref="SqliteMigrationLedger"/> against a real temp-file database — no Docker, no
/// shared fixture, one file per test instance.
/// </summary>
public sealed class SqliteMigrationLedgerTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"sqlbound-ledger-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    [Fact]
    public async Task Should_CreateLedgerAndReadEmpty_When_NoMigrationsApplied()
    {
        await using var connection = await OpenAsync();
        var ledger = new SqliteMigrationLedger();

        await ledger.EnsureCreatedAsync(connection, Token);

        Assert.Empty(await ledger.GetAppliedAsync(connection, Token));
    }

    [Fact]
    public async Task Should_RecordAppliedMigrationsAndReadThemBackOrderedByVersion()
    {
        await using var connection = await OpenAsync();
        var ledger = new SqliteMigrationLedger();
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
        await using var connection = await OpenAsync();
        var ledger = new SqliteMigrationLedger();
        await ledger.EnsureCreatedAsync(connection, Token);
        var first = new AppliedMigration(20260712143000, "first", "aaa", new DateTime(2026, 7, 12, 14, 30, 0), 7);
        var second = new AppliedMigration(20260712150000, "second", "bbb", new DateTime(2026, 7, 12, 15, 0, 0), 12);
        await ledger.RecordAppliedAsync(connection, null, first, Token);
        await ledger.RecordAppliedAsync(connection, null, second, Token);

        await ledger.RemoveAsync(connection, null, second.Version, Token);

        Assert.Equal([first], await ledger.GetAppliedAsync(connection, Token));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _path }.ConnectionString);
        await connection.OpenAsync(Token);
        return connection;
    }
}
