namespace SqlBound.Generators.UnitTests;

public class SqlQueryGeneratorTests
{
    private const string Prelude = """
        using System.Collections.Generic;
        using System.Data.Common;
        using System.Threading;
        using System.Threading.Tasks;
        using SqlBound;

        namespace App;

        public sealed record Item(int Id, string Name, decimal? Price);

        """;

    [Fact]
    public void Should_GenerateNothing_When_NoMethodCarriesSqlQueryAttribute()
    {
        const string source = """
            namespace App;

            public static class NoQueries
            {
                public static int Add(int left, int right) => left + right;
            }
            """;

        var outcome = GeneratorHarness.Run(source);

        Assert.Empty(outcome.GeneratedSources);
        Assert.Empty(outcome.GeneratorDiagnostics);
    }

    [Fact]
    public void Should_ReportNoDiagnostics_When_MethodIsWellFormed()
    {
        const string source = Prelude + """
            public static partial class ItemQueries
            {
                [SqlQuery("SELECT id, name, price FROM items WHERE category = @category")]
                public static partial Task<IReadOnlyList<Item>> GetItemsAsync(
                    DbConnection connection,
                    DbTransaction? transaction,
                    string category,
                    CancellationToken cancellationToken = default);
            }
            """;

        var outcome = GeneratorHarness.Run(source);

        Assert.Empty(outcome.GeneratorDiagnostics);
    }

    [Fact]
    public void Should_ReportSqlb001_When_MethodIsNotPartial()
    {
        const string source = Prelude + """
            public static partial class ItemQueries
            {
                [SqlQuery("SELECT id, name, price FROM items")]
                public static Task<IReadOnlyList<Item>> GetItemsAsync(DbConnection connection)
                    => Task.FromResult<IReadOnlyList<Item>>([]);
            }
            """;

        var outcome = GeneratorHarness.Run(source);

        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("SQLB001", diagnostic.Id);
    }

    [Fact]
    public void Should_ReportSqlb002_When_MethodIsNotStatic()
    {
        const string source = Prelude + """
            public partial class ItemQueries
            {
                [SqlQuery("SELECT id, name, price FROM items")]
                public partial Task<IReadOnlyList<Item>> GetItemsAsync(DbConnection connection);
            }
            """;

        var outcome = GeneratorHarness.Run(source);

        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("SQLB002", diagnostic.Id);
    }

    [Fact]
    public void Should_ReportSqlb003_When_FirstParameterIsNotDbConnection()
    {
        const string source = Prelude + """
            public static partial class ItemQueries
            {
                [SqlQuery("SELECT id, name, price FROM items WHERE category = @category")]
                public static partial Task<IReadOnlyList<Item>> GetItemsAsync(string category);
            }
            """;

        var outcome = GeneratorHarness.Run(source);

        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("SQLB003", diagnostic.Id);
    }

    [Fact]
    public void Should_ReportSqlb004_When_ReturnTypeIsNotASupportedShape()
    {
        const string source = Prelude + """
            public static partial class ItemQueries
            {
                [SqlQuery("DELETE FROM items")]
                public static partial Task DeleteAllAsync(DbConnection connection);
            }
            """;

        var outcome = GeneratorHarness.Run(source);

        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("SQLB004", diagnostic.Id);
    }

    [Fact]
    public void Should_ReportSqlb005_When_RowTypeConstructorParameterTypeIsUnsupported()
    {
        const string source = Prelude + """
            public sealed record Exotic(int Id, object Payload);

            public static partial class ItemQueries
            {
                [SqlQuery("SELECT id, payload FROM exotics")]
                public static partial Task<IReadOnlyList<Exotic>> GetExoticsAsync(DbConnection connection);
            }
            """;

        var outcome = GeneratorHarness.Run(source);

        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("SQLB005", diagnostic.Id);
    }

    [Fact]
    public void Should_ReportSqlb006_When_QueryParameterTypeIsUnsupported()
    {
        const string source = Prelude + """
            public static partial class ItemQueries
            {
                [SqlQuery("SELECT id, name, price FROM items WHERE category = @category")]
                public static partial Task<IReadOnlyList<Item>> GetItemsAsync(
                    DbConnection connection, object category);
            }
            """;

        var outcome = GeneratorHarness.Run(source);

        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("SQLB006", diagnostic.Id);
    }

    [Fact]
    public void Should_ReportSqlb007_When_CommandTextIsWhitespace()
    {
        const string source = Prelude + """
            public static partial class ItemQueries
            {
                [SqlQuery("   ")]
                public static partial Task<IReadOnlyList<Item>> GetItemsAsync(DbConnection connection);
            }
            """;

        var outcome = GeneratorHarness.Run(source);

        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("SQLB007", diagnostic.Id);
    }

    [Fact]
    public void Should_ReportSqlb008_When_MethodIsGeneric()
    {
        const string source = Prelude + """
            public static partial class ItemQueries
            {
                [SqlQuery("SELECT id, name, price FROM items")]
                public static partial Task<IReadOnlyList<Item>> GetItemsAsync<TIgnored>(DbConnection connection);
            }
            """;

        var outcome = GeneratorHarness.Run(source);

        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("SQLB008", diagnostic.Id);
    }
}
