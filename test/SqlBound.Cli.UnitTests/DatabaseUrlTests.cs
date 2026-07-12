using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;

namespace SqlBound.Cli.UnitTests;

public sealed class DatabaseUrlTests
{
    [Fact]
    public void Should_ReturnValueVerbatimAsSqlServer_When_ValueIsAConnectionString()
    {
        const string value = "Server=localhost;Database=shop;User Id=sa;Password=secret";

        var target = DatabaseUrl.Resolve(value);

        Assert.Equal(DatabaseProviders.SqlServer, target.Provider);
        Assert.Equal(value, target.ConnectionString);
    }

    [Fact]
    public void Should_MapAllUrlParts_When_UrlIsCompleteSqlServer()
    {
        var target = DatabaseUrl.Resolve(
            "sqlserver://sa:P%40ssw0rd@db.example.com:14330/shop?TrustServerCertificate=true");

        Assert.Equal(DatabaseProviders.SqlServer, target.Provider);
        var builder = new SqlConnectionStringBuilder(target.ConnectionString);
        Assert.Equal("db.example.com,14330", builder.DataSource);
        Assert.Equal("shop", builder.InitialCatalog);
        Assert.Equal("sa", builder.UserID);
        Assert.Equal("P@ssw0rd", builder.Password);
        Assert.True(builder.TrustServerCertificate);
        Assert.False(builder.IntegratedSecurity);
    }

    [Fact]
    public void Should_UseIntegratedSecurity_When_SqlServerUrlCarriesNoCredentials()
    {
        var target = DatabaseUrl.Resolve("sqlserver://localhost/shop");

        var builder = new SqlConnectionStringBuilder(target.ConnectionString);
        Assert.Equal("localhost", builder.DataSource);
        Assert.Equal("shop", builder.InitialCatalog);
        Assert.True(builder.IntegratedSecurity);
    }

    [Fact]
    public void Should_OmitPortAndDatabase_When_SqlServerUrlOmitsThem()
    {
        var target = DatabaseUrl.Resolve("sqlserver://localhost");

        var builder = new SqlConnectionStringBuilder(target.ConnectionString);
        Assert.Equal("localhost", builder.DataSource);
        Assert.Equal(string.Empty, builder.InitialCatalog);
    }

    [Theory]
    [InlineData("this is not a connection string")]
    [InlineData("sqlserver://")]
    public void Should_ThrowArgumentException_When_ValueIsUnusable(string value)
    {
        Assert.Throws<ArgumentException>(() => DatabaseUrl.Resolve(value));
    }

    [Fact]
    public void Should_ThrowArgumentExceptionNamingTheOption_When_SqlServerUrlQueryHasUnknownOption()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => DatabaseUrl.Resolve("sqlserver://localhost/shop?NoSuchOption=1"));

        Assert.Contains("NoSuchOption", exception.Message);
    }

    [Fact]
    public void Should_UsePathAsDataSource_When_UrlIsSqlite()
    {
        var target = DatabaseUrl.Resolve("sqlite:///var/data/shop.db");

        Assert.Equal(DatabaseProviders.Sqlite, target.Provider);
        var builder = new SqliteConnectionStringBuilder(target.ConnectionString);
        Assert.Equal("/var/data/shop.db", builder.DataSource);
    }

    [Fact]
    public void Should_PreserveRelativePath_When_SqliteUrlUsesADottedPath()
    {
        var target = DatabaseUrl.Resolve("sqlite://./data/shop.db");

        var builder = new SqliteConnectionStringBuilder(target.ConnectionString);
        Assert.Equal("./data/shop.db", builder.DataSource);
    }

    [Fact]
    public void Should_ThrowArgumentException_When_SqliteUrlHasNoPath()
    {
        Assert.Throws<ArgumentException>(() => DatabaseUrl.Resolve("sqlite://"));
    }
}
