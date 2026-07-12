using System.CommandLine;
using Microsoft.Data.SqlClient;
using SqlBound.SqlServer;

namespace SqlBound.Cli;

/// <summary>
/// Builds the <c>database</c> command tree: <c>create</c> and <c>drop</c> for the target database
/// named by the connection string. SQL Server only in this release; the other providers follow in
/// M15 once the database-lifecycle abstraction is extracted alongside a second implementation.
/// </summary>
internal static class DatabaseCommand
{
    public static Command Build()
    {
        var connectionOption = new Option<string?>("--connection")
        {
            Description =
                $"Connection string or provider URL (default: the {DatabaseUrl.EnvironmentVariable} environment variable).",
        };

        var createCommand = new Command("create", "Create the target database if it does not already exist.")
        {
            connectionOption,
        };
        createCommand.SetAction((parseResult, cancellationToken) =>
            RunAsync(parseResult, connectionOption, create: true, cancellationToken));

        var dropCommand = new Command("drop", "Drop the target database if it exists.")
        {
            connectionOption,
        };
        dropCommand.SetAction((parseResult, cancellationToken) =>
            RunAsync(parseResult, connectionOption, create: false, cancellationToken));

        return new Command("database", "Create or drop the target database (SQL Server only in this release).")
        {
            createCommand,
            dropCommand,
        };
    }

    private static async Task<int> RunAsync(
        ParseResult parseResult, Option<string?> connectionOption, bool create, CancellationToken cancellationToken)
    {
        var databaseValue = parseResult.GetValue(connectionOption)
            ?? Environment.GetEnvironmentVariable(DatabaseUrl.EnvironmentVariable);
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
            Console.Error.WriteLine(
                $"error: 'database {(create ? "create" : "drop")}' currently supports SQL Server only.");
            return 1;
        }

        var admin = new SqlServerDatabaseAdmin();
        try
        {
            var name = create
                ? await admin.CreateAsync(target.ConnectionString, cancellationToken).ConfigureAwait(false)
                : await admin.DropAsync(target.ConnectionString, cancellationToken).ConfigureAwait(false);
            Console.Out.WriteLine(create ? $"database '{name}' is ready." : $"database '{name}' is dropped.");
            return 0;
        }
        catch (Exception exception) when (exception is SqlException or ArgumentException)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 1;
        }
    }
}
