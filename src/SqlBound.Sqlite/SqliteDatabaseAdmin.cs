using Microsoft.Data.Sqlite;
using SqlBound.Migrations;

namespace SqlBound.Sqlite;

/// <summary>
/// The SQLite implementation of <see cref="IDatabaseAdmin"/>. SQLite has no server: the "database"
/// is the file named by the connection string's <c>Data Source</c>. Creating it opens a connection
/// (which materializes the file); dropping it deletes the file. Both are idempotent.
/// </summary>
public sealed class SqliteDatabaseAdmin : IDatabaseAdmin
{
    /// <inheritdoc />
    public async Task<string> CreateAsync(string connectionString, CancellationToken cancellationToken)
    {
        var dataSource = DataSourceOf(connectionString);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return dataSource;
    }

    /// <inheritdoc />
    public Task<string> DropAsync(string connectionString, CancellationToken cancellationToken)
    {
        var dataSource = DataSourceOf(connectionString);

        // Release any pooled handles so the file is not locked when we delete it.
        SqliteConnection.ClearAllPools();
        if (File.Exists(dataSource))
        {
            File.Delete(dataSource);
        }

        return Task.FromResult(dataSource);
    }

    private static string DataSourceOf(string connectionString)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            throw new ArgumentException(
                "The connection string names no database (set Data Source).", nameof(connectionString));
        }

        return dataSource;
    }
}
