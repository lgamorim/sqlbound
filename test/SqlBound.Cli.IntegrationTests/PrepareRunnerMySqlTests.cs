namespace SqlBound.Cli.IntegrationTests;

/// <summary>
/// End-to-end prepare against a real MySQL container, closing the loop this milestone's provider
/// dispatch exists for: a <c>mysql://</c> connection URL routes to
/// <see cref="SqlBound.MySql.MySqlQueryDescriber"/> and the resulting snapshot carries the
/// "mysql" provider tag.
/// </summary>
public sealed class PrepareRunnerMySqlTests(PrepareMySqlFixture fixture)
    : IClassFixture<PrepareMySqlFixture>, IDisposable
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

    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("sqlbound-prepare-mysql-tests-");

    public void Dispose() => _project.Delete(recursive: true);

    [Fact]
    public async Task Should_WriteAMySqlTaggedSnapshot_When_ConnectionUrlUsesTheMysqlScheme()
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
        Assert.Contains("\"provider\": \"mysql\"", json);
        Assert.Contains("\"clrTypeText\": null", json);
    }
}
