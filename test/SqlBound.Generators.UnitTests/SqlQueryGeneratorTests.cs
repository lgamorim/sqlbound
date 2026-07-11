namespace SqlBound.Generators.UnitTests;

public class SqlQueryGeneratorTests
{
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
}
