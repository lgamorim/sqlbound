namespace SqlBound.Migrations;

/// <summary>
/// Creates and drops the database named by a connection string. Implementations connect to a
/// maintenance database (or, for a file-based engine, act on the file) and guard against a
/// connection string that names no database. Used by the CLI <c>database</c> command.
/// </summary>
public interface IDatabaseAdmin
{
    /// <summary>Creates the target database if it does not already exist.</summary>
    /// <param name="connectionString">A connection string whose target database is to be created.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The name of the target database.</returns>
    Task<string> CreateAsync(string connectionString, CancellationToken cancellationToken);

    /// <summary>Drops the target database if it exists.</summary>
    /// <param name="connectionString">A connection string whose target database is to be dropped.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The name of the target database.</returns>
    Task<string> DropAsync(string connectionString, CancellationToken cancellationToken);
}
