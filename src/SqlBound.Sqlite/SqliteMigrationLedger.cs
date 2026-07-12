using System.Data.Common;
using Microsoft.Data.Sqlite;
using SqlBound.Migrations;

namespace SqlBound.Sqlite;

/// <summary>
/// The SQLite implementation of <see cref="IMigrationLedger"/>. The ledger lives in the
/// <c>_sqlbound_migrations</c> table. SQLite has no date/time type, so <c>applied_on_utc</c> is
/// stored as ISO-8601 text; Microsoft.Data.Sqlite round-trips it through <see cref="DateTime"/>.
/// SQLite applies DDL within a transaction, so migrations are transactional.
/// </summary>
public sealed class SqliteMigrationLedger : IMigrationLedger
{
    private const string TableName = "_sqlbound_migrations";

    /// <inheritdoc />
    public bool SupportsTransactionalDdl => true;

    /// <inheritdoc />
    public async Task EnsureCreatedAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = AsSqliteConnection(connection).CreateCommand();
        command.CommandText =
            $"""
            CREATE TABLE IF NOT EXISTS {TableName} (
                version INTEGER NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                checksum TEXT NOT NULL,
                applied_on_utc TEXT NOT NULL,
                execution_ms INTEGER NOT NULL);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppliedMigration>> GetAppliedAsync(
        DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = AsSqliteConnection(connection).CreateCommand();
        command.CommandText =
            $"SELECT version, name, checksum, applied_on_utc, execution_ms FROM {TableName} ORDER BY version;";

        var applied = new List<AppliedMigration>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            applied.Add(new AppliedMigration(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDateTime(3),
                reader.GetInt64(4)));
        }

        return applied;
    }

    /// <inheritdoc />
    public async Task RecordAppliedAsync(
        DbConnection connection, DbTransaction? transaction, AppliedMigration migration, CancellationToken cancellationToken)
    {
        await using var command = AsSqliteConnection(connection).CreateCommand();
        command.Transaction = (SqliteTransaction?)transaction;
        command.CommandText =
            $"""
            INSERT INTO {TableName} (version, name, checksum, applied_on_utc, execution_ms)
            VALUES (@version, @name, @checksum, @appliedOnUtc, @executionMs);
            """;
        command.Parameters.AddWithValue("@version", migration.Version);
        command.Parameters.AddWithValue("@name", migration.Name);
        command.Parameters.AddWithValue("@checksum", migration.Checksum);
        command.Parameters.AddWithValue("@appliedOnUtc", migration.AppliedOnUtc);
        command.Parameters.AddWithValue("@executionMs", migration.ExecutionMs);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(
        DbConnection connection, DbTransaction? transaction, long version, CancellationToken cancellationToken)
    {
        await using var command = AsSqliteConnection(connection).CreateCommand();
        command.Transaction = (SqliteTransaction?)transaction;
        command.CommandText = $"DELETE FROM {TableName} WHERE version = @version;";
        command.Parameters.AddWithValue("@version", version);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static SqliteConnection AsSqliteConnection(DbConnection connection) =>
        connection as SqliteConnection
        ?? throw new ArgumentException(
            $"The SQLite migration ledger requires a {nameof(SqliteConnection)}.", nameof(connection));
}
