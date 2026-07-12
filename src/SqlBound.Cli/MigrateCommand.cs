using System.CommandLine;
using SqlBound.Migrations;

namespace SqlBound.Cli;

/// <summary>
/// Builds the <c>migrate</c> command tree: <c>add</c> scaffolds a migration, <c>run</c> applies the
/// pending ones. <c>revert</c> and <c>status</c> are added alongside <c>run</c> as M14 proceeds.
/// </summary>
internal static class MigrateCommand
{
    public static Command Build() =>
        new("migrate", "Author and apply SQL-file migrations.")
        {
            BuildAdd(),
            BuildRun(),
        };

    private static Command BuildAdd()
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = "A short description of the migration, e.g. \"create items\".",
        };
        var migrationsOption = MigrationsOption();
        var irreversibleOption = new Option<bool>("--irreversible")
        {
            Description = "Scaffold only the up script; the migration cannot be reverted.",
        };

        var addCommand = new Command(
            "add",
            "Scaffold a new timestamped migration: an up script and, unless --irreversible, a down script.")
        {
            nameArgument,
            migrationsOption,
            irreversibleOption,
        };
        addCommand.SetAction(parseResult =>
        {
            try
            {
                var created = MigrationScaffolder.Create(
                    ResolveDirectory(parseResult.GetValue(migrationsOption)),
                    parseResult.GetValue(nameArgument)!,
                    reversible: !parseResult.GetValue(irreversibleOption),
                    TimeProvider.System);
                foreach (var path in created)
                {
                    Console.Out.WriteLine($"created {path}");
                }

                return 0;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                Console.Error.WriteLine($"error: {exception.Message}");
                return 1;
            }
        });

        return addCommand;
    }

    private static Command BuildRun()
    {
        var migrationsOption = MigrationsOption();
        var connectionOption = ConnectionOption();

        var runCommand = new Command("run", "Apply all pending migrations, each in its own transaction.")
        {
            migrationsOption,
            connectionOption,
        };
        runCommand.SetAction((parseResult, cancellationToken) => MigrationCli.ExecuteAsync(
            parseResult.GetValue(connectionOption),
            ResolveDirectory(parseResult.GetValue(migrationsOption)),
            async (connection, ledger, migrations) =>
            {
                var applied = await MigrationRunner
                    .RunAsync(connection, ledger, migrations, TimeProvider.System, cancellationToken)
                    .ConfigureAwait(false);
                if (applied.Count == 0)
                {
                    Console.Out.WriteLine("already up to date; no migrations to apply.");
                    return 0;
                }

                foreach (var migration in applied)
                {
                    Console.Out.WriteLine($"applied {migration.Version}_{migration.Name} ({migration.ExecutionMs} ms)");
                }

                Console.Out.WriteLine($"applied {applied.Count} migration(s).");
                return 0;
            },
            cancellationToken));

        return runCommand;
    }

    private static Option<string?> MigrationsOption() =>
        new("--migrations") { Description = "The migrations directory (default: ./migrations)." };

    private static Option<string?> ConnectionOption() =>
        new("--connection")
        {
            Description =
                $"Connection string or provider URL (default: the {DatabaseUrl.EnvironmentVariable} environment variable).",
        };

    private static string ResolveDirectory(string? migrationsDirectory) =>
        string.IsNullOrWhiteSpace(migrationsDirectory)
            ? Path.Combine(Directory.GetCurrentDirectory(), "migrations")
            : migrationsDirectory;
}
