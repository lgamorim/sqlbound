namespace SqlBound.Cli.IntegrationTests;

public sealed class PrepareRunnerTests(PrepareSqlServerFixture fixture)
    : IClassFixture<PrepareSqlServerFixture>, IDisposable
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
            [SqlQuery("SELECT Id, Name, Price FROM dbo.Items WHERE Id = @id")]
            public static partial Task<Item> GetAsync(
                DbConnection connection, int id, CancellationToken cancellationToken = default);

            [SqlExecute("DELETE FROM dbo.Items WHERE Id = @id")]
            public static partial Task<int> DeleteAsync(
                DbConnection connection, int id, CancellationToken cancellationToken = default);
        }

        public sealed record Item(int Id, string Name, decimal? Price);
        """;

    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("sqlbound-prepare-tests-");

    private string SnapshotDirectory => Path.Combine(_project.FullName, ".sqlbound");

    public void Dispose() => _project.Delete(recursive: true);

    [Fact]
    public async Task Should_WriteOneSnapshotPerQueryAndExitZero_When_PreparingSucceeds()
    {
        WriteSource(Source);

        var (exitCode, output) = await RunPrepareAsync();

        Assert.Equal(0, exitCode);
        var files = Directory.GetFiles(SnapshotDirectory, "query-*.json");
        Assert.Equal(2, files.Length);
        Assert.Contains(files, file => File.ReadAllText(file).Contains("SELECT Id, Name, Price FROM dbo.Items"));
        Assert.Contains("2 added", output);
    }

    [Fact]
    public async Task Should_ExitZeroOnCleanCheckAndTwoOnDrift_When_QueriesChange()
    {
        WriteSource(Source);
        await RunPrepareAsync();

        var (cleanExit, _) = await RunPrepareAsync(check: true);
        WriteSource(Source.Replace("SELECT Id, Name, Price", "SELECT Id, Name"));
        var (driftExit, driftOutput) = await RunPrepareAsync(check: true);

        Assert.Equal(0, cleanExit);
        Assert.Equal(2, driftExit);
        Assert.Contains("stale", driftOutput);
        // --check must not have touched the committed snapshots.
        Assert.Equal(2, Directory.GetFiles(SnapshotDirectory, "query-*.json").Length);
    }

    [Fact]
    public async Task Should_PruneOrphanedSnapshots_When_AQueryIsRemoved()
    {
        WriteSource(Source);
        await RunPrepareAsync();

        var withoutDelete = Source
            .Replace(
                """
                    [SqlExecute("DELETE FROM dbo.Items WHERE Id = @id")]
                    public static partial Task<int> DeleteAsync(
                        DbConnection connection, int id, CancellationToken cancellationToken = default);
                """,
                string.Empty);
        WriteSource(withoutDelete);
        var (exitCode, output) = await RunPrepareAsync();

        Assert.Equal(0, exitCode);
        Assert.Single(Directory.GetFiles(SnapshotDirectory, "query-*.json"));
        Assert.Contains("1 removed", output);
    }

    [Fact]
    public async Task Should_ExitOneAndLeaveDiskUntouched_When_AStatementCannotBeDescribed()
    {
        WriteSource(Source);
        await RunPrepareAsync();

        var broken = Source.Replace("FROM dbo.Items WHERE Id = @id\")]", "FROM dbo.NoSuchTable WHERE Id = @id\")]");
        WriteSource(broken);
        var (exitCode, output) = await RunPrepareAsync();

        Assert.Equal(1, exitCode);
        Assert.Contains("GetAsync", output);
        // A failed run must not prune or rewrite snapshots from the last good run.
        Assert.Equal(2, Directory.GetFiles(SnapshotDirectory, "query-*.json").Length);
    }

    [Fact]
    public async Task Should_PassAnalyzerVerification_When_SnapshotsComeFromARealPrepare()
    {
        WriteSource(Source);
        var (exitCode, _) = await RunPrepareAsync();
        Assert.Equal(0, exitCode);

        var cleanDiagnostics = await VerificationAnalyzerRunner.RunAsync(Source, SnapshotDirectory);
        Assert.Empty(cleanDiagnostics);

        // The same snapshots must flag a declaration that stopped matching the database.
        var nonNullablePrice = Source.Replace("decimal? Price", "decimal Price");
        var driftDiagnostics = await VerificationAnalyzerRunner.RunAsync(nonNullablePrice, SnapshotDirectory);
        var diagnostic = Assert.Single(driftDiagnostics);
        Assert.Equal("SQLB106", diagnostic.Id);
    }

    private void WriteSource(string source) =>
        File.WriteAllText(Path.Combine(_project.FullName, "ItemQueries.cs"), source);

    private async Task<(int ExitCode, string Output)> RunPrepareAsync(bool check = false)
    {
        await using var output = new StringWriter();
        var exitCode = await PrepareRunner.RunAsync(
            _project.FullName,
            fixture.GetConnectionString(),
            check,
            output,
            TestContext.Current.CancellationToken);
        return (exitCode, output.ToString());
    }
}
