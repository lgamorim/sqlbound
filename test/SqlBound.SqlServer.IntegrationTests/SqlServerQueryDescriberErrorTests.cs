using Microsoft.Data.SqlClient;
using SqlBound.Introspection;

namespace SqlBound.SqlServer.IntegrationTests;

public sealed class SqlServerQueryDescriberErrorTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Should_ThrowDescribeExceptionWithServerError_When_SqlHasSyntaxError()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        const string commandText = "SELEC Id FROM dbo.Items";

        var exception = await Assert.ThrowsAsync<SqlBoundDescribeException>(
            () => new SqlServerQueryDescriber().DescribeAsync(
                connection, commandText, TestContext.Current.CancellationToken));

        Assert.Equal(commandText, exception.CommandText);
        Assert.IsType<SqlException>(exception.InnerException);
        Assert.Contains("Incorrect syntax", exception.Message);
    }

    [Fact]
    public async Task Should_ThrowDescribeException_When_TableDoesNotExist()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        const string commandText = "SELECT Id FROM dbo.NoSuchTable";

        var exception = await Assert.ThrowsAsync<SqlBoundDescribeException>(
            () => new SqlServerQueryDescriber().DescribeAsync(
                connection, commandText, TestContext.Current.CancellationToken));

        Assert.Equal(commandText, exception.CommandText);
        Assert.Contains("NoSuchTable", exception.Message);
    }

    [Fact]
    public async Task Should_ThrowDescribeException_When_StatementUsesTempTable()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        const string commandText = "SELECT Id INTO #scratch FROM dbo.Items; SELECT Id FROM #scratch";

        var exception = await Assert.ThrowsAsync<SqlBoundDescribeException>(
            () => new SqlServerQueryDescriber().DescribeAsync(
                connection, commandText, TestContext.Current.CancellationToken));

        Assert.Equal(commandText, exception.CommandText);
    }

    [Fact]
    public async Task Should_ThrowDescribeException_When_ParameterUseIsAmbiguous()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        const string commandText = "SELECT Id FROM dbo.Items WHERE Id = @p AND Name = @p";

        var exception = await Assert.ThrowsAsync<SqlBoundDescribeException>(
            () => new SqlServerQueryDescriber().DescribeAsync(
                connection, commandText, TestContext.Current.CancellationToken));

        Assert.Equal(commandText, exception.CommandText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_ThrowArgumentException_When_CommandTextIsNullOrWhiteSpace(string? commandText)
    {
        await using var connection = new SqlConnection();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => new SqlServerQueryDescriber().DescribeAsync(connection, commandText!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowArgumentNullException_When_ConnectionIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => new SqlServerQueryDescriber().DescribeAsync(null!, "SELECT 1", TestContext.Current.CancellationToken));
    }
}
