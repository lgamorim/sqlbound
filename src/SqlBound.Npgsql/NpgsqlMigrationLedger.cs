using System.Data.Common;
using global::Npgsql;
using SqlBound.Migrations;

namespace SqlBound.Npgsql;

/// <summary>
/// The PostgreSQL implementation of <see cref="IMigrationLedger"/>. The ledger lives in the
/// <c>_sqlbound_migrations</c> table; <c>applied_on_utc</c> is a <c>timestamptz</c>, which Npgsql
/// pairs with a <see cref="DateTime"/> of <see cref="DateTimeKind.Utc"/>. PostgreSQL applies DDL
/// within a transaction, so migrations are transactional.
/// </summary>
public sealed class NpgsqlMigrationLedger : IMigrationLedger
{
    private const string TableName = "_sqlbound_migrations";

    /// <inheritdoc />
    public bool SupportsTransactionalDdl => true;

    /// <inheritdoc />
    public async Task EnsureCreatedAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = AsNpgsqlConnection(connection).CreateCommand();
        command.CommandText =
            $"""
            CREATE TABLE IF NOT EXISTS {TableName} (
                version bigint NOT NULL PRIMARY KEY,
                name text NOT NULL,
                checksum varchar(64) NOT NULL,
                applied_on_utc timestamptz NOT NULL,
                execution_ms bigint NOT NULL);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppliedMigration>> GetAppliedAsync(
        DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = AsNpgsqlConnection(connection).CreateCommand();
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
        await using var command = AsNpgsqlConnection(connection).CreateCommand();
        command.Transaction = (NpgsqlTransaction?)transaction;
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
        await using var command = AsNpgsqlConnection(connection).CreateCommand();
        command.Transaction = (NpgsqlTransaction?)transaction;
        command.CommandText = $"DELETE FROM {TableName} WHERE version = @version;";
        command.Parameters.AddWithValue("@version", version);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static NpgsqlConnection AsNpgsqlConnection(DbConnection connection) =>
        connection as NpgsqlConnection
        ?? throw new ArgumentException(
            $"The PostgreSQL migration ledger requires an {nameof(NpgsqlConnection)}.", nameof(connection));
}
