using Microsoft.CodeAnalysis;

namespace SqlBound.Generators.UnitTests;

public class SqlExecuteGeneratorTests
{
    private const string Prelude = """
        using System.Collections.Generic;
        using System.Data.Common;
        using System.Threading;
        using System.Threading.Tasks;
        using SqlBound;

        namespace App;

        """;

    [Fact]
    public void Should_ReturnRowsAffected_When_ExecuteReturnsTaskOfInt()
    {
        const string source = Prelude + """
            public static partial class ItemCommands
            {
                [SqlExecute("DELETE FROM items WHERE category = @category")]
                public static partial Task<int> DeleteCategoryAsync(
                    DbConnection connection, string category, CancellationToken cancellationToken = default);
            }
            """;

        var outcome = GeneratorHarness.Run(source);

        AssertCompilesClean(outcome);
        var generated = Assert.Single(outcome.GeneratedSources).SourceText.ToString();
        Assert.Contains("return await __command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);", generated);
    }

    [Fact]
    public void Should_DiscardRowsAffected_When_ExecuteReturnsPlainTask()
    {
        const string source = Prelude + """
            public static partial class ItemCommands
            {
                [SqlExecute("DELETE FROM items")]
                public static partial Task ClearAsync(
                    DbConnection connection, CancellationToken cancellationToken = default);
            }
            """;

        var outcome = GeneratorHarness.Run(source);

        AssertCompilesClean(outcome);
        var generated = Assert.Single(outcome.GeneratedSources).SourceText.ToString();
        Assert.Contains("await __command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);", generated);
        Assert.DoesNotContain("return await", generated);
    }

    [Fact]
    public void Should_BindScalarsAndTransaction_When_ExecuteHasParameters()
    {
        const string source = Prelude + """
            public static partial class ItemCommands
            {
                [SqlExecute("UPDATE items SET price = @price WHERE id = @id")]
                public static partial Task<int> RepriceAsync(
                    DbConnection connection, DbTransaction? transaction, int id, decimal price,
                    CancellationToken cancellationToken = default);
            }
            """;

        var outcome = GeneratorHarness.Run(source);

        AssertCompilesClean(outcome);
        var generated = Assert.Single(outcome.GeneratedSources).SourceText.ToString();
        Assert.Contains("""ParameterName = "@id";""", generated);
        Assert.Contains("""ParameterName = "@price";""", generated);
        Assert.Contains("__command.Transaction = transaction;", generated);
    }

    [Fact]
    public void Should_ReportSqlb009_When_ExecuteReturnTypeIsUnsupported()
    {
        const string source = Prelude + """
            public static partial class ItemCommands
            {
                [SqlExecute("DELETE FROM items")]
                public static partial Task<string> ClearAsync(DbConnection connection);
            }
            """;

        var outcome = GeneratorHarness.Run(source);

        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("SQLB009", diagnostic.Id);
    }

    [Fact]
    public void Should_ReportSqlb010AndGenerateNothing_When_MethodCarriesBothAttributes()
    {
        const string source = Prelude + """
            public static partial class ItemCommands
            {
                [SqlQuery("SELECT COUNT(*) FROM items")]
                [SqlExecute("DELETE FROM items")]
                public static partial Task<int> ConfusedAsync(DbConnection connection);
            }
            """;

        var outcome = GeneratorHarness.Run(source);

        var diagnostic = Assert.Single(outcome.GeneratorDiagnostics);
        Assert.Equal("SQLB010", diagnostic.Id);
        Assert.Empty(outcome.GeneratedSources);
    }

    private static void AssertCompilesClean(GeneratorRunOutcome outcome)
    {
        Assert.Empty(outcome.GeneratorDiagnostics);
        Assert.DoesNotContain(outcome.CompilationDiagnostics, d => d.Severity >= DiagnosticSeverity.Warning);
    }
}
