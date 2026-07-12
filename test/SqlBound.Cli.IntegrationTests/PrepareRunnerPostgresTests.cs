namespace SqlBound.Cli.IntegrationTests;

/// <summary>
/// End-to-end prepare against a real Postgres container, closing the loop this milestone's
/// provider dispatch exists for: a <c>postgresql://</c> connection URL routes to
/// <see cref="SqlBound.Npgsql.NpgsqlQueryDescriber"/> and the resulting snapshot carries the
/// "postgres" provider tag.
/// </summary>
public sealed class PrepareRunnerPostgresTests(PreparePostgresFixture fixture)
    : IClassFixture<PreparePostgresFixture>, IDisposable
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
            [SqlQuery("SELECT id, name, price FROM items WHERE id = @id")]
            public static partial Task<Item> GetAsync(
                DbConnection connection, int id, CancellationToken cancellationToken = default);
        }

        public sealed record Item(int Id, string Name, decimal? Price);
        """;

    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("sqlbound-prepare-postgres-tests-");

    public void Dispose() => _project.Delete(recursive: true);

    [Fact]
    public async Task Should_WriteAPostgresTaggedSnapshot_When_ConnectionUrlUsesThePostgresqlScheme()
    {
        File.WriteAllText(Path.Combine(_project.FullName, "ItemQueries.cs"), Source);

        await using var output = new StringWriter();
        var exitCode = await PrepareRunner.RunAsync(
            _project.FullName,
            fixture.GetConnectionUrl(),
            check: false,
            output,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains("1 added", output.ToString());
        var snapshotDirectory = Path.Combine(_project.FullName, ".sqlbound");
        var file = Assert.Single(Directory.GetFiles(snapshotDirectory, "query-*.json"));
        var json = File.ReadAllText(file);
        Assert.Contains("\"provider\": \"postgres\"", json);
        Assert.Contains("\"name\": \"price\"", json);
    }
}
