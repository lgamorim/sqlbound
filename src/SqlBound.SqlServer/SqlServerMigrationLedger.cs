using System.Data.Common;
using Microsoft.Data.SqlClient;
using SqlBound.Migrations;

namespace SqlBound.SqlServer;

/// <summary>
/// The SQL Server implementation of <see cref="IMigrationLedger"/>. The ledger lives in
/// <c>dbo._sqlbound_migrations</c> and is created on demand. Reads are ordered by version so the
/// caller sees the applied history in the same order the migrations directory presents it.
/// </summary>
public sealed class SqlServerMigrationLedger : IMigrationLedger
{
    private const string TableName = "_sqlbound_migrations";

    /// <inheritdoc />
    public async Task EnsureCreatedAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = AsSqlConnection(connection).CreateCommand();
        command.CommandText =
            $"""
            IF OBJECT_ID(N'dbo.{TableName}', N'U') IS NULL
            CREATE TABLE dbo.{TableName} (
                version BIGINT NOT NULL CONSTRAINT PK_{TableName} PRIMARY KEY,
                name NVARCHAR(200) NOT NULL,
                checksum CHAR(64) NOT NULL,
                applied_on_utc DATETIME2 NOT NULL,
                execution_ms BIGINT NOT NULL);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppliedMigration>> GetAppliedAsync(
        DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = AsSqlConnection(connection).CreateCommand();
        command.CommandText =
            $"SELECT version, name, checksum, applied_on_utc, execution_ms FROM dbo.{TableName} ORDER BY version;";

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
        await using var command = AsSqlConnection(connection).CreateCommand();
        command.Transaction = (SqlTransaction?)transaction;
        command.CommandText =
            $"""
            INSERT INTO dbo.{TableName} (version, name, checksum, applied_on_utc, execution_ms)
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
        await using var command = AsSqlConnection(connection).CreateCommand();
        command.Transaction = (SqlTransaction?)transaction;
        command.CommandText = $"DELETE FROM dbo.{TableName} WHERE version = @version;";
        command.Parameters.AddWithValue("@version", version);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static SqlConnection AsSqlConnection(DbConnection connection) =>
        connection as SqlConnection
        ?? throw new ArgumentException(
            $"The SQL Server migration ledger requires a {nameof(SqlConnection)}.", nameof(connection));
}
