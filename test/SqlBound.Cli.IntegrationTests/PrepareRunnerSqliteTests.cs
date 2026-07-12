using Microsoft.Data.Sqlite;

namespace SqlBound.Cli.IntegrationTests;

/// <summary>
/// End-to-end prepare against a real SQLite file, closing the loop this milestone's provider
/// dispatch exists for: a <c>sqlite://</c> connection URL routes to <see cref="SqlBound.Sqlite.SqliteQueryDescriber"/>
/// and the resulting snapshot carries the "sqlite" provider tag. Needs no Docker - SQLite is
/// embedded - so, unlike the SQL Server prepare tests, this needs no fixture-level skip.
/// </summary>
public sealed class PrepareRunnerSqliteTests : IDisposable
{
    private const string Source =
        """
        using System.Collections.Generic;
        using System.Data.Common;
        using System.Threading;
        using System.Threading.Tasks;
        using SqlBound;

        namespace App;

        public static partial class ItemQueries
        {
            [SqlQuery("SELECT id, name FROM items WHERE id = @id")]
            public static partial Task<Item> GetAsync(
                DbConnection connection, int id, CancellationToken cancellationToken = default);
        }

        public sealed record Item(int Id, string Name);
        """;

    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("sqlbound-prepare-sqlite-tests-");
    private readonly string _databasePath;

    public PrepareRunnerSqliteTests()
    {
        _databasePath = Path.Combine(_project.FullName, "app.db");
        using var connection = new SqliteConnection($"Data Source={_databasePath}");
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE items (id INTEGER NOT NULL PRIMARY KEY, name TEXT NOT NULL);";
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite pools the native handle by default; clear it or the temp
        // directory delete below races the pooled file lock.
        SqliteConnection.ClearAllPools();
        _project.Delete(recursive: true);
    }

    [Fact]
    public async Task Should_WriteASqliteTaggedSnapshot_When_ConnectionUrlUsesTheSqliteScheme()
    {
        File.WriteAllText(Path.Combine(_project.FullName, "ItemQueries.cs"), Source);

        await using var output = new StringWriter();
        var exitCode = await PrepareRunner.RunAsync(
            _project.FullName,
            $"sqlite://{_databasePath.Replace('\\', '/')}",
            check: false,
            output,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("1 added", output.ToString());
        var snapshotDirectory = Path.Combine(_project.FullName, ".sqlbound");
        var file = Assert.Single(Directory.GetFiles(snapshotDirectory, "query-*.json"));
        var json = File.ReadAllText(file);
        Assert.Contains("\"provider\": \"sqlite\"", json);
        Assert.Contains("\"clrTypeText\": null", json);
    }
}
