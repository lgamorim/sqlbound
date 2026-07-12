using MySqlConnector;
using SqlBound.Migrations;

namespace SqlBound.MySql;

/// <summary>
/// The MySQL implementation of <see cref="IDatabaseAdmin"/>. Connects without a default database to
/// create or drop the target named by the connection string's <c>Database</c>, using
/// <c>CREATE/DROP DATABASE IF (NOT) EXISTS</c> for idempotency. Refuses a connection string that
/// names no database or a system database. Identifiers are backtick-quoted.
/// </summary>
public sealed class MySqlDatabaseAdmin : IDatabaseAdmin
{
    private static readonly string[] SystemDatabases =
        ["mysql", "information_schema", "performance_schema", "sys"];

    /// <inheritdoc />
    public Task<string> CreateAsync(string connectionString, CancellationToken cancellationToken) =>
        ExecuteAsync(connectionString, "CREATE DATABASE IF NOT EXISTS", cancellationToken);

    /// <inheritdoc />
    public Task<string> DropAsync(string connectionString, CancellationToken cancellationToken) =>
        ExecuteAsync(connectionString, "DROP DATABASE IF EXISTS", cancellationToken);

    private static async Task<string> ExecuteAsync(
        string connectionString, string statement, CancellationToken cancellationToken)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        var database = builder.Database;
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new ArgumentException(
                "The connection string names no database (set Database).", nameof(connectionString));
        }

        if (SystemDatabases.Contains(database, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Refusing to operate on the system database '{database}'.", nameof(connectionString));
        }

        builder.Database = string.Empty;
        await using var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{statement} {Quote(database)};";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return database;
    }

    private static string Quote(string identifier) => $"`{identifier.Replace("`", "``")}`";
}
