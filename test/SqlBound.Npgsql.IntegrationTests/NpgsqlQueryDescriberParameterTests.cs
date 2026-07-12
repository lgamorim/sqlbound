using SqlBound.Introspection;

namespace SqlBound.Npgsql.IntegrationTests;

public sealed class NpgsqlQueryDescriberParameterTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Should_DescribeParameterNamesAndTypesInOrder_When_CommandUsesPlaceholders()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new NpgsqlQueryDescriber().DescribeAsync(
            connection,
            "SELECT id FROM items WHERE id = @id AND name = @name AND price > @minPrice",
            TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new DescribedParameter("id", "integer", "int"),
                new DescribedParameter("name", "text", "string"),
                new DescribedParameter("minPrice", "numeric", "decimal"),
            ],
            description.Parameters);
    }

    [Fact]
    public async Task Should_DescribeParameters_When_StatementProducesNoResultSet()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new NpgsqlQueryDescriber().DescribeAsync(
            connection, "DELETE FROM items WHERE id = @id", TestContext.Current.CancellationToken);

        Assert.Empty(description.Columns);
        var parameter = Assert.Single(description.Parameters);
        Assert.Equal(new DescribedParameter("id", "integer", "int"), parameter);
    }

    [Fact]
    public async Task Should_ReturnEmptyParameters_When_CommandHasNoPlaceholders()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new NpgsqlQueryDescriber().DescribeAsync(
            connection, "SELECT id FROM items", TestContext.Current.CancellationToken);

        Assert.Empty(description.Parameters);
    }

    [Fact]
    public async Task Should_ThrowDescribeException_When_ParameterUseIsAmbiguous()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        const string commandText = "SELECT id FROM items WHERE id = @p OR name = @p";

        var exception = await Assert.ThrowsAsync<SqlBoundDescribeException>(
            () => new NpgsqlQueryDescriber().DescribeAsync(
                connection, commandText, TestContext.Current.CancellationToken));

        Assert.Equal(commandText, exception.CommandText);
    }
}
