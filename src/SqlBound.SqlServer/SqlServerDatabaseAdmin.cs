using Microsoft.Data.SqlClient;
using SqlBound.Migrations;

namespace SqlBound.SqlServer;

/// <summary>
/// Creates and drops the database named by a connection string's <c>Initial Catalog</c>, connecting
/// to <c>master</c> to do so. Both operations are idempotent and guard against acting on a
/// connection string that names no database or names a system database. The target name is bracketed
/// with <c>QUOTENAME</c> server-side, so it can never be interpreted as SQL.
/// </summary>
public sealed class SqlServerDatabaseAdmin : IDatabaseAdmin
{
    private static readonly string[] SystemDatabases = ["master", "model", "msdb", "tempdb"];

    /// <inheritdoc />
    public Task<string> CreateAsync(string connectionString, CancellationToken cancellationToken) =>
        ExecuteAgainstMasterAsync(
            connectionString,
            """
            IF DB_ID(@name) IS NULL
            BEGIN
                DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@name);
                EXEC(@sql);
            END
            """,
            cancellationToken);

    /// <inheritdoc />
    public Task<string> DropAsync(string connectionString, CancellationToken cancellationToken) =>
        ExecuteAgainstMasterAsync(
            connectionString,
            """
            IF DB_ID(@name) IS NOT NULL
            BEGIN
                DECLARE @sql nvarchar(max) =
                    N'ALTER DATABASE ' + QUOTENAME(@name) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE;' +
                    N'DROP DATABASE ' + QUOTENAME(@name) + N';';
                EXEC(@sql);
            END
            """,
            cancellationToken);

    private static async Task<string> ExecuteAgainstMasterAsync(
        string connectionString, string commandText, CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var database = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new ArgumentException(
                "The connection string names no database (set Initial Catalog / Database).", nameof(connectionString));
        }

        if (SystemDatabases.Contains(database, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Refusing to operate on the system database '{database}'.", nameof(connectionString));
        }

        builder.InitialCatalog = "master";
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("@name", database);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return database;
    }
}
