namespace SqlBound.SqlServer.IntegrationTests;

public sealed class SqlServerQueryDescriberParameterTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Should_DescribeParameterNamesAndTypesInOrder_When_CommandUsesPlaceholders()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await SqlServerQueryDescriber.DescribeAsync(
            connection,
            "SELECT Id FROM dbo.Items WHERE Id = @id AND Name = @name AND Price > @minPrice",
            TestContext.Current.CancellationToken);

        // @minPrice sits in a comparison, so SQL Server suggests the widened decimal(38,19)
        // rather than the column's decimal(18,2) - suggested types are inferences, not lookups.
        Assert.Equal(
            [
                new DescribedParameter("id", "int", "int"),
                new DescribedParameter("name", "nvarchar(50)", "string"),
                new DescribedParameter("minPrice", "decimal(38,19)", "decimal"),
            ],
            description.Parameters);
    }

    [Fact]
    public async Task Should_DescribeParameters_When_StatementProducesNoResultSet()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await SqlServerQueryDescriber.DescribeAsync(
            connection, "DELETE FROM dbo.Items WHERE Id = @id", TestContext.Current.CancellationToken);

        Assert.Empty(description.Columns);
        var parameter = Assert.Single(description.Parameters);
        Assert.Equal(new DescribedParameter("id", "int", "int"), parameter);
    }

    [Fact]
    public async Task Should_ReturnEmptyParameters_When_CommandHasNoPlaceholders()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await SqlServerQueryDescriber.DescribeAsync(
            connection, "SELECT Id FROM dbo.Items", TestContext.Current.CancellationToken);

        Assert.Empty(description.Parameters);
    }

    [Fact]
    public async Task Should_ThrowDescribeException_When_ParameterTypeHasNoMapping()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        const string commandText = "SELECT IntCol FROM dbo.EveryType WHERE DateTimeOffsetCol = @moment";

        var exception = await Assert.ThrowsAsync<SqlBoundDescribeException>(
            () => SqlServerQueryDescriber.DescribeAsync(
                connection, commandText, TestContext.Current.CancellationToken));

        Assert.Contains("datetimeoffset", exception.Message);
        Assert.Equal(commandText, exception.CommandText);
    }
}
