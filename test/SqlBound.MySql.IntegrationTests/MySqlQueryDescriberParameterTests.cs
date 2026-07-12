using SqlBound.Introspection;

namespace SqlBound.MySql.IntegrationTests;

public sealed class MySqlQueryDescriberParameterTests(MySqlFixture fixture)
{
    [Fact]
    public async Task Should_DescribeParameterNamesInOrder_When_CommandUsesPlaceholders()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new MySqlQueryDescriber().DescribeAsync(
            connection,
            "SELECT id FROM items WHERE id = @id AND name = @name AND price > @minPrice",
            TestContext.Current.CancellationToken);

        // MySQL has no static parameter typing: the server echoes back whatever type the caller
        // declares, so there is nothing genuine to report - sqlTypeName is empty and
        // clrTypeText is null, matching SQLite's provider.
        Assert.Equal(
            [
                new DescribedParameter("id", string.Empty, ClrTypeText: null),
                new DescribedParameter("name", string.Empty, ClrTypeText: null),
                new DescribedParameter("minPrice", string.Empty, ClrTypeText: null),
            ],
            description.Parameters);
    }

    [Fact]
    public async Task Should_DescribeParameters_When_StatementProducesNoResultSet()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new MySqlQueryDescriber().DescribeAsync(
            connection, "DELETE FROM items WHERE id = @id", TestContext.Current.CancellationToken);

        Assert.Empty(description.Columns);
        var parameter = Assert.Single(description.Parameters);
        Assert.Equal(new DescribedParameter("id", string.Empty, ClrTypeText: null), parameter);
    }

    [Fact]
    public async Task Should_ReturnEmptyParameters_When_CommandHasNoPlaceholders()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new MySqlQueryDescriber().DescribeAsync(
            connection, "SELECT id FROM items", TestContext.Current.CancellationToken);

        Assert.Empty(description.Parameters);
    }

    [Fact]
    public async Task Should_ReportOneParameter_When_TheSamePlaceholderIsUsedMultipleTimes()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new MySqlQueryDescriber().DescribeAsync(
            connection, "SELECT id FROM items WHERE id = @p OR name = @p", TestContext.Current.CancellationToken);

        var parameter = Assert.Single(description.Parameters);
        Assert.Equal("p", parameter.Name);
    }
}
