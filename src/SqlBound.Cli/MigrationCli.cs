using System.Data.Common;
using SqlBound.Migrations;

namespace SqlBound.Cli;

/// <summary>
/// Shared plumbing for the connected <c>migrate</c> subcommands (<c>run</c>, <c>revert</c>,
/// <c>status</c>): resolve the target, load the migrations directory, open the connection, build the
/// provider's ledger, and invoke the command's action — translating every expected failure into an
/// error message and a non-zero exit code.
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
        IMigrationLedger ledger;
        try
        {
            target = DatabaseUrl.Resolve(databaseValue);
            ledger = ProviderServices.Ledger(target.Provider);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
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

        var connection = ProviderServices.CreateConnection(target);
        await using (connection.ConfigureAwait(false))
        {
            try
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is DbException or InvalidOperationException)
            {
                Console.Error.WriteLine($"error: cannot connect to the database: {exception.Message}");
                return 1;
            }

            try
            {
                return await action(connection, ledger, migrations).ConfigureAwait(false);
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
