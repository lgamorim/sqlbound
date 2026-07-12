using SqlBound.Introspection;

namespace SqlBound.Sqlite.IntegrationTests;

public sealed class SqliteQueryDescriberErrorTests(SqliteFixture fixture)
{
    [Fact]
    public async Task Should_ThrowDescribeException_When_SqlHasSyntaxError()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        const string commandText = "SELEC id FROM items";

        var exception = await Assert.ThrowsAsync<SqlBoundDescribeException>(
            () => new SqliteQueryDescriber().DescribeAsync(
                connection, commandText, TestContext.Current.CancellationToken));

        Assert.Equal(commandText, exception.CommandText);
        Assert.Contains("syntax error", exception.Message);
    }

    [Fact]
    public async Task Should_ThrowDescribeException_When_TableDoesNotExist()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        const string commandText = "SELECT id FROM no_such_table";

        var exception = await Assert.ThrowsAsync<SqlBoundDescribeException>(
            () => new SqliteQueryDescriber().DescribeAsync(
                connection, commandText, TestContext.Current.CancellationToken));

        Assert.Equal(commandText, exception.CommandText);
        Assert.Contains("no_such_table", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_ThrowArgumentException_When_CommandTextIsNullOrWhiteSpace(string? commandText)
    {
        await using var connection = await fixture.OpenConnectionAsync();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => new SqliteQueryDescriber().DescribeAsync(
                connection, commandText!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowArgumentNullException_When_ConnectionIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => new SqliteQueryDescriber().DescribeAsync(null!, "SELECT 1", TestContext.Current.CancellationToken));
    }
}
