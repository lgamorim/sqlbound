namespace SqlBound.Cli.UnitTests;

public sealed class QueryDiscoveryTests
{
    [Fact]
    public void Should_DiscoverCommandTextAndMethod_When_AttributeUsesARegularLiteral()
    {
        var result = QueryDiscovery.DiscoverFromSource(
            """
            using SqlBound;

            public static partial class ItemQueries
            {
                [SqlQuery("SELECT Id FROM dbo.Items")]
                public static partial Task<IReadOnlyList<int>> GetIdsAsync(DbConnection connection);
            }
            """,
            "ItemQueries.cs");

        var query = Assert.Single(result.Queries);
        Assert.Equal("SELECT Id FROM dbo.Items", query.CommandText);
        Assert.Equal("GetIdsAsync", query.MethodName);
        Assert.Equal("ItemQueries.cs", query.FilePath);
        Assert.False(query.IsExecute);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Should_DiscoverCommandText_When_AttributeUsesVerbatimAndRawLiterals()
    {
        var result = QueryDiscovery.DiscoverFromSource(
            """"
            using SqlBound;

            public static partial class ItemQueries
            {
                [SqlQuery(@"SELECT Id
            FROM dbo.Items")]
                public static partial Task<IReadOnlyList<int>> GetIdsAsync(DbConnection connection);

                [SqlQuery("""
                    SELECT Name FROM dbo.Items
                    """)]
                public static partial Task<IReadOnlyList<string>> GetNamesAsync(DbConnection connection);
            }
            """",
            "ItemQueries.cs");

        Assert.Equal(2, result.Queries.Count);
        Assert.Equal("SELECT Id\nFROM dbo.Items", result.Queries[0].CommandText.Replace("\r\n", "\n"));
        Assert.Equal("SELECT Name FROM dbo.Items", result.Queries[1].CommandText);
    }

    [Fact]
    public void Should_ConcatenateParts_When_CommandTextIsBuiltFromLiteralConcatenation()
    {
        var result = QueryDiscovery.DiscoverFromSource(
            """
            using SqlBound;

            public static partial class ItemQueries
            {
                [SqlQuery("SELECT Id " + "FROM dbo.Items " + "WHERE Id = @id")]
                public static partial Task<int> GetAsync(DbConnection connection, int id);
            }
            """,
            "ItemQueries.cs");

        var query = Assert.Single(result.Queries);
        Assert.Equal("SELECT Id FROM dbo.Items WHERE Id = @id", query.CommandText);
    }

    [Fact]
    public void Should_MarkQueryAsExecute_When_AttributeIsSqlExecute()
    {
        var result = QueryDiscovery.DiscoverFromSource(
            """
            using SqlBound;

            public static partial class ItemCommands
            {
                [SqlExecute("DELETE FROM dbo.Items")]
                public static partial Task<int> DeleteAllAsync(DbConnection connection);
            }
            """,
            "ItemCommands.cs");

        var query = Assert.Single(result.Queries);
        Assert.True(query.IsExecute);
    }

    [Theory]
    [InlineData("SqlBound.SqlQuery")]
    [InlineData("SqlQueryAttribute")]
    public void Should_DiscoverQuery_When_AttributeNameIsQualifiedOrSuffixed(string attributeName)
    {
        var result = QueryDiscovery.DiscoverFromSource(
            $$"""
            public static partial class ItemQueries
            {
                [{{attributeName}}("SELECT 1")]
                public static partial Task<int> GetOneAsync(DbConnection connection);
            }
            """,
            "ItemQueries.cs");

        var query = Assert.Single(result.Queries);
        Assert.Equal("SELECT 1", query.CommandText);
    }

    [Fact]
    public void Should_WarnAndSkip_When_CommandTextIsNotALiteral()
    {
        var result = QueryDiscovery.DiscoverFromSource(
            """
            using SqlBound;

            public static partial class ItemQueries
            {
                private const string Sql = "SELECT Id FROM dbo.Items";

                [SqlQuery(Sql)]
                public static partial Task<IReadOnlyList<int>> GetIdsAsync(DbConnection connection);

                [SqlQuery($"SELECT {1}")]
                public static partial Task<int> GetInterpolatedAsync(DbConnection connection);
            }
            """,
            "ItemQueries.cs");

        Assert.Empty(result.Queries);
        Assert.Equal(2, result.Warnings.Count);
        Assert.Contains("GetIdsAsync", result.Warnings[0]);
        Assert.Contains("ItemQueries.cs", result.Warnings[0]);
        Assert.Contains("GetInterpolatedAsync", result.Warnings[1]);
    }

    [Fact]
    public void Should_IgnoreOtherAttributes_When_ScanningSource()
    {
        var result = QueryDiscovery.DiscoverFromSource(
            """
            public static class ItemQueries
            {
                [Obsolete("SELECT looking string")]
                public static void OldMethod() { }
            }
            """,
            "ItemQueries.cs");

        Assert.Empty(result.Queries);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Should_DiscoverAllQueries_When_MethodsSpanNestedTypes()
    {
        var result = QueryDiscovery.DiscoverFromSource(
            """
            using SqlBound;

            namespace App;

            public static partial class Outer
            {
                [SqlQuery("SELECT 1")]
                public static partial Task<int> FirstAsync(DbConnection connection);

                public static partial class Inner
                {
                    [SqlExecute("DELETE FROM dbo.Items")]
                    public static partial Task<int> SecondAsync(DbConnection connection);
                }
            }
            """,
            "Queries.cs");

        Assert.Equal(2, result.Queries.Count);
        Assert.Equal(["FirstAsync", "SecondAsync"], result.Queries.Select(q => q.MethodName));
    }
}
