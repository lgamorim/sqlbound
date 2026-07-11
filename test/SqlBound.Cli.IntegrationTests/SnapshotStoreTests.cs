namespace SqlBound.Cli.IntegrationTests;

public sealed class SnapshotStoreTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("sqlbound-store-tests-");

    private string StorePath => Path.Combine(_directory.FullName, ".sqlbound");

    public void Dispose() => _directory.Delete(recursive: true);

    [Fact]
    public void Should_CreateDirectoryAndWriteAllFiles_When_ApplyingToAnEmptyProject()
    {
        var desired = Desired(("query-a.json", "{ \"a\": 1 }\n"), ("query-b.json", "{ \"b\": 2 }\n"));

        var difference = SnapshotStore.Apply(StorePath, desired);

        Assert.Equal(["query-a.json", "query-b.json"], difference.Added);
        Assert.Empty(difference.Changed);
        Assert.Empty(difference.Orphaned);
        Assert.Equal("{ \"a\": 1 }\n", File.ReadAllText(Path.Combine(StorePath, "query-a.json")));
    }

    [Fact]
    public void Should_ReportEverythingUnchanged_When_ApplyingTheSameStateTwice()
    {
        var desired = Desired(("query-a.json", "{ \"a\": 1 }\n"));
        SnapshotStore.Apply(StorePath, desired);

        var difference = SnapshotStore.Apply(StorePath, desired);

        Assert.Empty(difference.Added);
        Assert.Empty(difference.Changed);
        Assert.Equal(["query-a.json"], difference.Unchanged);
        Assert.False(difference.HasDrift);
    }

    [Fact]
    public void Should_RewriteChangedAndDeleteOrphanedFiles_When_QueriesEvolve()
    {
        SnapshotStore.Apply(StorePath, Desired(
            ("query-a.json", "{ \"a\": 1 }\n"), ("query-gone.json", "{ \"old\": true }\n")));

        var difference = SnapshotStore.Apply(StorePath, Desired(("query-a.json", "{ \"a\": 2 }\n")));

        Assert.Equal(["query-a.json"], difference.Changed);
        Assert.Equal(["query-gone.json"], difference.Orphaned);
        Assert.Equal("{ \"a\": 2 }\n", File.ReadAllText(Path.Combine(StorePath, "query-a.json")));
        Assert.False(File.Exists(Path.Combine(StorePath, "query-gone.json")));
    }

    [Fact]
    public void Should_LeaveDiskUntouched_When_Comparing()
    {
        SnapshotStore.Apply(StorePath, Desired(("query-old.json", "{ \"old\": true }\n")));

        var difference = SnapshotStore.Compare(StorePath, Desired(("query-new.json", "{ \"new\": true }\n")));

        Assert.Equal(["query-new.json"], difference.Added);
        Assert.Equal(["query-old.json"], difference.Orphaned);
        Assert.True(difference.HasDrift);
        Assert.True(File.Exists(Path.Combine(StorePath, "query-old.json")));
        Assert.False(File.Exists(Path.Combine(StorePath, "query-new.json")));
    }

    [Fact]
    public void Should_TreatContentAsUnchanged_When_DiskFileHasWindowsLineEndings()
    {
        // Committed snapshots may be checked out with CRLF by git; that is not drift.
        Directory.CreateDirectory(StorePath);
        File.WriteAllText(Path.Combine(StorePath, "query-a.json"), "{\r\n  \"a\": 1\r\n}\r\n");

        var difference = SnapshotStore.Compare(StorePath, Desired(("query-a.json", "{\n  \"a\": 1\n}\n")));

        Assert.False(difference.HasDrift);
    }

    [Fact]
    public void Should_IgnoreForeignFiles_When_PruningOrphans()
    {
        Directory.CreateDirectory(StorePath);
        File.WriteAllText(Path.Combine(StorePath, "README.md"), "not a snapshot");

        var difference = SnapshotStore.Apply(StorePath, Desired(("query-a.json", "{ \"a\": 1 }\n")));

        Assert.Empty(difference.Orphaned);
        Assert.True(File.Exists(Path.Combine(StorePath, "README.md")));
    }

    private static Dictionary<string, string> Desired(params (string FileName, string Content)[] files) =>
        files.ToDictionary(file => file.FileName, file => file.Content);
}
