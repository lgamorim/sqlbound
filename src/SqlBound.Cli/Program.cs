using System.CommandLine;
using SqlBound.Cli;

var projectOption = new Option<string>("--project")
{
    Description = "The project directory to scan for [SqlQuery]/[SqlExecute] methods (default: current directory).",
};
var connectionOption = new Option<string?>("--connection")
{
    Description = $"Connection string, sqlserver:// URL, sqlite:// URL, postgresql:// URL, or mysql:// URL (default: the {DatabaseUrl.EnvironmentVariable} environment variable).",
};
var checkOption = new Option<bool>("--check")
{
    Description = "Verify the committed snapshots instead of writing: exit 2 if any are stale.",
};

var prepareCommand = new Command(
    "prepare",
    "Describe every [SqlQuery]/[SqlExecute] statement against the database and write .sqlbound/ snapshots.")
{
    projectOption,
    connectionOption,
    checkOption,
};
prepareCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var databaseValue = parseResult.GetValue(connectionOption)
        ?? Environment.GetEnvironmentVariable(DatabaseUrl.EnvironmentVariable);
    if (string.IsNullOrWhiteSpace(databaseValue))
    {
        Console.Error.WriteLine($"error: set {DatabaseUrl.EnvironmentVariable} or pass --connection.");
        return 1;
    }

    return await PrepareRunner.RunAsync(
        parseResult.GetValue(projectOption) ?? Directory.GetCurrentDirectory(),
        databaseValue,
        parseResult.GetValue(checkOption),
        Console.Out,
        cancellationToken);
});

var root = new RootCommand("SqlBound: compile-time verified SQL for .NET.")
{
    prepareCommand,
    MigrateCommand.Build(),
};
return await root.Parse(args).InvokeAsync();
