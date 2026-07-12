using global::Npgsql;
using SqlBound.Migrations;

namespace SqlBound.Npgsql;

/// <summary>
/// The PostgreSQL implementation of <see cref="IDatabaseAdmin"/>. Connects to the maintenance
/// <c>postgres</c> database to create or drop the target named by the connection string's
/// <c>Database</c>. Both operations are idempotent and refuse a connection string that names no
/// database or a system database. Drop uses <c>WITH (FORCE)</c> to evict open connections.
/// </summary>
public sealed class NpgsqlDatabaseAdmin : IDatabaseAdmin
{
    private const string MaintenanceDatabase = "postgres";
    private static readonly string[] SystemDatabases = ["postgres", "template0", "template1"];

    /// <inheritdoc />
    public async Task<string> CreateAsync(string connectionString, CancellationToken cancellationToken)
    {
        var database = DatabaseOf(connectionString);
        await using var connection = await OpenMaintenanceAsync(connectionString, cancellationToken).ConfigureAwait(false);

        await using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name;";
        exists.Parameters.AddWithValue("@name", database);
        if (await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
        {
            await using var create = connection.CreateCommand();
            create.CommandText = $"CREATE DATABASE {Quote(database)};";
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return database;
    }

    /// <inheritdoc />
    public async Task<string> DropAsync(string connectionString, CancellationToken cancellationToken)
    {
        var database = DatabaseOf(connectionString);
        await using var connection = await OpenMaintenanceAsync(connectionString, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS {Quote(database)} WITH (FORCE);";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return database;
    }

    private static async Task<NpgsqlConnection> OpenMaintenanceAsync(
        string connectionString, CancellationToken cancellationToken)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = MaintenanceDatabase };
        var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static string DatabaseOf(string connectionString)
    {
        var database = new NpgsqlConnectionStringBuilder(connectionString).Database;
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

        return database;
    }

    private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";
}
