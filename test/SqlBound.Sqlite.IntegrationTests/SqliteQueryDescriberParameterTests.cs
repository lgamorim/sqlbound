using SqlBound.Introspection;

namespace SqlBound.Sqlite.IntegrationTests;

public sealed class SqliteQueryDescriberParameterTests(SqliteFixture fixture)
{
    [Fact]
    public async Task Should_DescribeParameterNamesInOrder_When_CommandUsesPlaceholders()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new SqliteQueryDescriber().DescribeAsync(
            connection,
            "SELECT id FROM items WHERE id = @id AND name = @name",
            TestContext.Current.CancellationToken);

        // SQLite has no static parameter typing: sqlTypeName is empty and clrTypeText is null,
        // unlike SQL Server which can suggest a type for each undeclared parameter.
        Assert.Equal(
            [
                new DescribedParameter("id", string.Empty, ClrTypeText: null),
                new DescribedParameter("name", string.Empty, ClrTypeText: null),
            ],
            description.Parameters);
    }

    [Fact]
    public async Task Should_DescribeParameters_When_StatementProducesNoResultSet()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new SqliteQueryDescriber().DescribeAsync(
            connection, "DELETE FROM items WHERE id = @id", TestContext.Current.CancellationToken);

        Assert.Empty(description.Columns);
        var parameter = Assert.Single(description.Parameters);
        Assert.Equal(new DescribedParameter("id", string.Empty, ClrTypeText: null), parameter);
    }

    [Fact]
    public async Task Should_ReturnEmptyParameters_When_CommandHasNoPlaceholders()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new SqliteQueryDescriber().DescribeAsync(
            connection, "SELECT id FROM items", TestContext.Current.CancellationToken);

        Assert.Empty(description.Parameters);
    }

    [Fact]
    public async Task Should_ThrowDescribeException_When_ParameterIsPositionalWithoutAName()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        const string commandText = "SELECT id FROM items WHERE id = ?";

        var exception = await Assert.ThrowsAsync<SqlBoundDescribeException>(
            () => new SqliteQueryDescriber().DescribeAsync(
                connection, commandText, TestContext.Current.CancellationToken));

        Assert.Equal(commandText, exception.CommandText);
    }
}
