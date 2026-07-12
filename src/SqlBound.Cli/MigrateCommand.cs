using System.CommandLine;

namespace SqlBound.Cli;

/// <summary>
/// Builds the <c>migrate</c> command tree. M13 ships <c>migrate add</c> (scaffold a new migration);
/// <c>run</c>, <c>revert</c>, and <c>status</c> follow in M14.
/// </summary>
internal static class MigrateCommand
{
    public static Command Build()
    {
        var nameArgument = new Argument<string>("name")
        {
            Description = "A short description of the migration, e.g. \"create items\".",
        };
        var migrationsOption = new Option<string?>("--migrations")
        {
            Description = "The migrations directory (default: ./migrations).",
        };
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
            var directory = parseResult.GetValue(migrationsOption);
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = Path.Combine(Directory.GetCurrentDirectory(), "migrations");
            }

            try
            {
                var created = MigrationScaffolder.Create(
                    directory,
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

        return new Command("migrate", "Author and apply SQL-file migrations.")
        {
            addCommand,
        };
    }
}
