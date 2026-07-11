namespace SqlBound.SqlServer.UnitTests;

public sealed class SqlServerTypeMapTests
{
    [Theory]
    [InlineData("bit", "bool")]
    [InlineData("tinyint", "byte")]
    [InlineData("smallint", "short")]
    [InlineData("int", "int")]
    [InlineData("bigint", "long")]
    [InlineData("real", "float")]
    [InlineData("float", "double")]
    [InlineData("decimal(18,2)", "decimal")]
    [InlineData("numeric(10,4)", "decimal")]
    [InlineData("money", "decimal")]
    [InlineData("smallmoney", "decimal")]
    [InlineData("char(10)", "string")]
    [InlineData("varchar(50)", "string")]
    [InlineData("varchar(max)", "string")]
    [InlineData("nchar(10)", "string")]
    [InlineData("nvarchar(50)", "string")]
    [InlineData("nvarchar(max)", "string")]
    [InlineData("text", "string")]
    [InlineData("ntext", "string")]
    [InlineData("binary(16)", "byte[]")]
    [InlineData("varbinary(50)", "byte[]")]
    [InlineData("varbinary(max)", "byte[]")]
    [InlineData("image", "byte[]")]
    [InlineData("rowversion", "byte[]")]
    [InlineData("timestamp", "byte[]")]
    [InlineData("uniqueidentifier", "global::System.Guid")]
    [InlineData("date", "global::System.DateTime")]
    [InlineData("smalldatetime", "global::System.DateTime")]
    [InlineData("datetime", "global::System.DateTime")]
    [InlineData("datetime2", "global::System.DateTime")]
    [InlineData("datetime2(7)", "global::System.DateTime")]
    public void Should_MapToGeneratorTypeText_When_SqlTypeIsSupported(string sqlTypeName, string expected)
    {
        var mapped = SqlServerTypeMap.TryMap(sqlTypeName, out var clrTypeText);

        Assert.True(mapped);
        Assert.Equal(expected, clrTypeText);
    }

    [Theory]
    [InlineData("datetimeoffset(7)")]
    [InlineData("time(7)")]
    [InlineData("sql_variant")]
    [InlineData("xml")]
    [InlineData("geography")]
    [InlineData("geometry")]
    [InlineData("hierarchyid")]
    [InlineData("sometype")]
    [InlineData("")]
    public void Should_RejectType_When_SqlTypeHasNoSupportedGetter(string sqlTypeName)
    {
        var mapped = SqlServerTypeMap.TryMap(sqlTypeName, out var clrTypeText);

        Assert.False(mapped);
        Assert.Null(clrTypeText);
    }

    [Theory]
    [InlineData("INT", "int")]
    [InlineData("NVarChar(50)", "string")]
    public void Should_MapIgnoringCase_When_SqlTypeNameIsNotLowerCase(string sqlTypeName, string expected)
    {
        var mapped = SqlServerTypeMap.TryMap(sqlTypeName, out var clrTypeText);

        Assert.True(mapped);
        Assert.Equal(expected, clrTypeText);
    }
}
