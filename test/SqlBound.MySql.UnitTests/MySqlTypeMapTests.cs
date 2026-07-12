namespace SqlBound.MySql.UnitTests;

public sealed class MySqlTypeMapTests
{
    [Theory]
    [InlineData("BOOL", "bool")]
    [InlineData("BOOLEAN", "bool")]
    [InlineData("TINYINT", "byte")]
    [InlineData("SMALLINT", "short")]
    [InlineData("MEDIUMINT", "int")]
    [InlineData("INT", "int")]
    [InlineData("INTEGER", "int")]
    [InlineData("BIGINT", "long")]
    [InlineData("FLOAT", "float")]
    [InlineData("DOUBLE", "double")]
    [InlineData("DECIMAL", "decimal")]
    [InlineData("DECIMAL(18,2)", "decimal")]
    [InlineData("NUMERIC", "decimal")]
    [InlineData("CHAR", "string")]
    [InlineData("CHAR(10)", "string")]
    [InlineData("VARCHAR", "string")]
    [InlineData("BLOB", "byte[]")]
    [InlineData("DATE", "global::System.DateTime")]
    [InlineData("DATETIME", "global::System.DateTime")]
    [InlineData("TIMESTAMP", "global::System.DateTime")]
    public void Should_MapToGeneratorTypeText_When_MySqlTypeIsSupported(string dataTypeName, string expected)
    {
        var mapped = MySqlTypeMap.TryMap(dataTypeName, out var clrTypeText);

        Assert.True(mapped);
        Assert.Equal(expected, clrTypeText);
    }

    [Theory]
    [InlineData("TIME")]
    [InlineData("YEAR")]
    [InlineData("BIT")]
    [InlineData("JSON")]
    [InlineData("ENUM")]
    [InlineData("SET")]
    [InlineData("SOMETYPE")]
    [InlineData("")]
    public void Should_RejectType_When_MySqlTypeHasNoSupportedGetter(string dataTypeName)
    {
        var mapped = MySqlTypeMap.TryMap(dataTypeName, out var clrTypeText);

        Assert.False(mapped);
        Assert.Null(clrTypeText);
    }

    [Theory]
    [InlineData("int", "int")]
    [InlineData("varchar", "string")]
    public void Should_MapIgnoringCase_When_MySqlTypeIsNotUpperCase(string dataTypeName, string expected)
    {
        var mapped = MySqlTypeMap.TryMap(dataTypeName, out var clrTypeText);

        Assert.True(mapped);
        Assert.Equal(expected, clrTypeText);
    }
}
