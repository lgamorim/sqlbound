namespace SqlBound.Generators.UnitTests;

public sealed class SqlQueryVerificationAnalyzerTests
{
    private const string CommandText = "SELECT Id, Name FROM dbo.Items WHERE Id = @id";

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
            [SqlQuery("SELECT Id, Name FROM dbo.Items WHERE Id = @id")]
            public static partial Task<IReadOnlyList<Item>> GetItemsAsync(
                DbConnection connection, int id, CancellationToken cancellationToken = default);
        }

        public sealed record Item(int Id, string Name);
        """;

    private const string MatchingSnapshot =
        """
        {
          "commandText": "SELECT Id, Name FROM dbo.Items WHERE Id = @id",
          "provider": "sqlserver",
          "columns": [
            { "ordinal": 0, "name": "Id", "sqlTypeName": "int", "clrTypeText": "int", "isNullable": false },
            { "ordinal": 1, "name": "Name", "sqlTypeName": "nvarchar(50)", "clrTypeText": "string", "isNullable": false }
          ],
          "parameters": [
            { "name": "id", "sqlTypeName": "int", "clrTypeText": "int" }
          ]
        }
        """;

    private static string SnapshotPath(string commandText) =>
        $"C:/repo/.sqlbound/query-{SnapshotKey.Compute(commandText)}.json";

    [Fact]
    public async Task Should_ReportNothing_When_NoSnapshotFilesAreWired()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(Source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Should_ReportNothing_When_AdditionalFilesAreNotSqlBoundSnapshots()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            Source, ("C:/repo/docs/notes.json", """{ "unrelated": true }"""));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Should_ReportNothing_When_SnapshotMatchesDeclaration()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            Source, (SnapshotPath(CommandText), MatchingSnapshot));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Should_ReportMissingSnapshot_When_ProjectHasSnapshotsButNotForThisQuery()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            Source,
            (SnapshotPath("SELECT 1"),
                """{ "commandText": "SELECT 1", "provider": "sqlserver", "columns": [], "parameters": [] }"""));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SQLB101", diagnostic.Id);
        Assert.Contains("GetItemsAsync", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Should_ReportInvalidSnapshot_When_FileIsMalformed()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            Source, (SnapshotPath(CommandText), "{ not json"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SQLB102", diagnostic.Id);
    }

    [Fact]
    public async Task Should_ReportInvalidSnapshot_When_EmbeddedCommandTextDiffersFromAttribute()
    {
        var staleSnapshot = MatchingSnapshot.Replace(
            "SELECT Id, Name FROM dbo.Items WHERE Id = @id",
            "SELECT Id FROM dbo.Items WHERE Id = @id");
        var diagnostics = await AnalyzerHarness.RunAsync(
            Source, (SnapshotPath(CommandText), staleSnapshot));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SQLB102", diagnostic.Id);
    }

    [Fact]
    public async Task Should_ReportVerificationFindingsAtTheMethod_When_SnapshotDisagrees()
    {
        var mismatchingSnapshot = MatchingSnapshot.Replace(
            "\"clrTypeText\": \"string\", \"isNullable\": false",
            "\"clrTypeText\": \"string\", \"isNullable\": true");
        var diagnostics = await AnalyzerHarness.RunAsync(
            Source, (SnapshotPath(CommandText), mismatchingSnapshot));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SQLB106", diagnostic.Id);
        var squiggled = diagnostic.Location.SourceTree!
            .GetText(TestContext.Current.CancellationToken)
            .ToString(diagnostic.Location.SourceSpan);
        Assert.Equal("GetItemsAsync", squiggled);
    }

    [Fact]
    public async Task Should_ReportNothing_When_MethodHasUsageErrors()
    {
        const string invalidSource =
            """
            using System.Collections.Generic;
            using System.Data.Common;
            using System.Threading.Tasks;
            using SqlBound;

            namespace App;

            public static class ItemQueries
            {
                [SqlQuery("SELECT Id FROM dbo.Items")]
                public static Task<IReadOnlyList<int>> GetIdsAsync(DbConnection connection) =>
                    Task.FromResult<IReadOnlyList<int>>([]);
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync(
            invalidSource, (SnapshotPath(CommandText), MatchingSnapshot));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Should_ReportExecuteResultSet_When_ExecuteSnapshotHasColumns()
    {
        const string executeSource =
            """
            using System.Data.Common;
            using System.Threading.Tasks;
            using SqlBound;

            namespace App;

            public static partial class ItemCommands
            {
                [SqlExecute("DELETE FROM dbo.Items OUTPUT DELETED.Id")]
                public static partial Task<int> DeleteAllAsync(DbConnection connection);
            }
            """;
        var snapshot =
            """
            {
              "commandText": "DELETE FROM dbo.Items OUTPUT DELETED.Id",
              "provider": "sqlserver",
              "columns": [ { "ordinal": 0, "name": "Id", "sqlTypeName": "int", "clrTypeText": "int", "isNullable": false } ],
              "parameters": []
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync(
            executeSource, (SnapshotPath("DELETE FROM dbo.Items OUTPUT DELETED.Id"), snapshot));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SQLB111", diagnostic.Id);
    }
}
