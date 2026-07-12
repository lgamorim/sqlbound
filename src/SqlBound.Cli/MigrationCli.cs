using System.Data.Common;
using Microsoft.Data.SqlClient;
using SqlBound.Migrations;
using SqlBound.SqlServer;

namespace SqlBound.Cli;

/// <summary>
/// Shared plumbing for the connected <c>migrate</c> subcommands (<c>run</c>, <c>revert</c>,
/// <c>status</c>): resolve the target, load the migrations directory, open the connection, build the
/// SQL Server ledger, and invoke the command's action — translating every expected failure into an
/// error message and a non-zero exit code. SQL Server only in this release.
/// </summary>
internal static class MigrationCli
{
    public static async Task<int> ExecuteAsync(
        string? connectionValue,
        string migrationsDirectory,
        Func<DbConnection, IMigrationLedger, IReadOnlyList<Migration>, Task<int>> action,
        CancellationToken cancellationToken)
    {
        var databaseValue = connectionValue ?? Environment.GetEnvironmentVariable(DatabaseUrl.EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(databaseValue))
        {
            Console.Error.WriteLine($"error: set {DatabaseUrl.EnvironmentVariable} or pass --connection.");
            return 1;
        }

        DatabaseTarget target;
        try
        {
            target = DatabaseUrl.Resolve(databaseValue);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 1;
        }

        if (target.Provider != DatabaseProviders.SqlServer)
        {
            Console.Error.WriteLine("error: 'migrate' currently supports SQL Server only.");
            return 1;
        }

        IReadOnlyList<Migration> migrations;
        try
        {
            migrations = MigrationDirectory.Load(migrationsDirectory);
        }
        catch (Exception exception) when (exception is MigrationFormatException or DirectoryNotFoundException)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 1;
        }

        var connection = new SqlConnection(target.ConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            try
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is SqlException or InvalidOperationException)
            {
                Console.Error.WriteLine($"error: cannot connect to the database: {exception.Message}");
                return 1;
            }

            try
            {
                return await action(connection, new SqlServerMigrationLedger(), migrations).ConfigureAwait(false);
            }
            catch (Exception exception)
                when (exception is MigrationInconsistencyException or MigrationExecutionException or DbException)
            {
                Console.Error.WriteLine($"error: {exception.Message}");
                return 1;
            }
        }
    }
}
