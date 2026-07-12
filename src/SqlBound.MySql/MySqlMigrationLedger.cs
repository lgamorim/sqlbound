using System.Data.Common;
using MySqlConnector;
using SqlBound.Migrations;

namespace SqlBound.MySql;

/// <summary>
/// The MySQL implementation of <see cref="IMigrationLedger"/>. The ledger lives in the
/// <c>_sqlbound_migrations</c> table. MySQL commits DDL implicitly (see ADR 0007), so
/// <see cref="SupportsTransactionalDdl"/> is <see langword="false"/> and <c>migrate run</c> applies
/// each migration without a transaction.
/// </summary>
public sealed class MySqlMigrationLedger : IMigrationLedger
{
    private const string TableName = "_sqlbound_migrations";

    /// <inheritdoc />
    public bool SupportsTransactionalDdl => false;

    /// <inheritdoc />
    public async Task EnsureCreatedAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = AsMySqlConnection(connection).CreateCommand();
        command.CommandText =
            $"""
            CREATE TABLE IF NOT EXISTS {TableName} (
                version BIGINT NOT NULL PRIMARY KEY,
                name VARCHAR(200) NOT NULL,
                checksum VARCHAR(64) NOT NULL,
                applied_on_utc DATETIME(6) NOT NULL,
                execution_ms BIGINT NOT NULL);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppliedMigration>> GetAppliedAsync(
        DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = AsMySqlConnection(connection).CreateCommand();
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
        await using var command = AsMySqlConnection(connection).CreateCommand();
        command.Transaction = (MySqlTransaction?)transaction;
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
        await using var command = AsMySqlConnection(connection).CreateCommand();
        command.Transaction = (MySqlTransaction?)transaction;
        command.CommandText = $"DELETE FROM {TableName} WHERE version = @version;";
        command.Parameters.AddWithValue("@version", version);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static MySqlConnection AsMySqlConnection(DbConnection connection) =>
        connection as MySqlConnection
        ?? throw new ArgumentException(
            $"The MySQL migration ledger requires a {nameof(MySqlConnection)}.", nameof(connection));
}
