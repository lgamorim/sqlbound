using global::Npgsql;
using SqlBound.Introspection;

namespace SqlBound.Npgsql.IntegrationTests;

public sealed class NpgsqlQueryDescriberErrorTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Should_ThrowDescribeExceptionWithServerError_When_SqlHasSyntaxError()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        const string commandText = "SELEC id FROM items";

        var exception = await Assert.ThrowsAsync<SqlBoundDescribeException>(
            () => new NpgsqlQueryDescriber().DescribeAsync(
                connection, commandText, TestContext.Current.CancellationToken));

        Assert.Equal(commandText, exception.CommandText);
        Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Contains("syntax error", exception.Message);
    }

    [Fact]
    public async Task Should_ThrowDescribeException_When_TableDoesNotExist()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        const string commandText = "SELECT id FROM no_such_table";

        var exception = await Assert.ThrowsAsync<SqlBoundDescribeException>(
            () => new NpgsqlQueryDescriber().DescribeAsync(
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
        await using var connection = new NpgsqlConnection();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => new NpgsqlQueryDescriber().DescribeAsync(connection, commandText!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowArgumentNullException_When_ConnectionIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => new NpgsqlQueryDescriber().DescribeAsync(null!, "SELECT 1", TestContext.Current.CancellationToken));
    }
}
