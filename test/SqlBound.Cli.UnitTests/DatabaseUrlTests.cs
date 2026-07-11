using Microsoft.Data.SqlClient;

namespace SqlBound.Cli.UnitTests;

public sealed class DatabaseUrlTests
{
    [Fact]
    public void Should_ReturnValueVerbatim_When_ValueIsAConnectionString()
    {
        const string value = "Server=localhost;Database=shop;User Id=sa;Password=secret";

        Assert.Equal(value, DatabaseUrl.ToConnectionString(value));
    }

    [Fact]
    public void Should_MapAllUrlParts_When_UrlIsComplete()
    {
        var result = DatabaseUrl.ToConnectionString(
            "sqlserver://sa:P%40ssw0rd@db.example.com:14330/shop?TrustServerCertificate=true");

        var builder = new SqlConnectionStringBuilder(result);
        Assert.Equal("db.example.com,14330", builder.DataSource);
        Assert.Equal("shop", builder.InitialCatalog);
        Assert.Equal("sa", builder.UserID);
        Assert.Equal("P@ssw0rd", builder.Password);
        Assert.True(builder.TrustServerCertificate);
        Assert.False(builder.IntegratedSecurity);
    }

    [Fact]
    public void Should_UseIntegratedSecurity_When_UrlCarriesNoCredentials()
    {
        var result = DatabaseUrl.ToConnectionString("sqlserver://localhost/shop");

        var builder = new SqlConnectionStringBuilder(result);
        Assert.Equal("localhost", builder.DataSource);
        Assert.Equal("shop", builder.InitialCatalog);
        Assert.True(builder.IntegratedSecurity);
    }

    [Fact]
    public void Should_OmitPortAndDatabase_When_UrlOmitsThem()
    {
        var result = DatabaseUrl.ToConnectionString("sqlserver://localhost");

        var builder = new SqlConnectionStringBuilder(result);
        Assert.Equal("localhost", builder.DataSource);
        Assert.Equal(string.Empty, builder.InitialCatalog);
    }

    [Theory]
    [InlineData("this is not a connection string")]
    [InlineData("sqlserver://")]
    public void Should_ThrowArgumentException_When_ValueIsUnusable(string value)
    {
        Assert.Throws<ArgumentException>(() => DatabaseUrl.ToConnectionString(value));
    }

    [Fact]
    public void Should_ThrowArgumentExceptionNamingTheOption_When_UrlQueryHasUnknownOption()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => DatabaseUrl.ToConnectionString("sqlserver://localhost/shop?NoSuchOption=1"));

        Assert.Contains("NoSuchOption", exception.Message);
    }
}
