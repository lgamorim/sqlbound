using Microsoft.Data.SqlClient;

namespace SqlBound.SqlServer.UnitTests;

public sealed class SqlServerDatabaseAdminGuardTests
{
    [Fact]
    public async Task Should_ThrowArgumentException_When_ConnectionStringNamesNoDatabase()
    {
        var connectionString = new SqlConnectionStringBuilder { DataSource = "localhost" }.ConnectionString;

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => new SqlServerDatabaseAdmin().CreateAsync(connectionString, TestContext.Current.CancellationToken));

        Assert.Equal("connectionString", exception.ParamName);
    }

    [Theory]
    [InlineData("master")]
    [InlineData("tempdb")]
    [InlineData("MSDB")]
    public async Task Should_ThrowArgumentException_When_TargetIsSystemDatabase(string database)
    {
        var connectionString =
            new SqlConnectionStringBuilder { DataSource = "localhost", InitialCatalog = database }.ConnectionString;

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => new SqlServerDatabaseAdmin().DropAsync(connectionString, TestContext.Current.CancellationToken));

        Assert.Contains(database, exception.Message);
    }
}
