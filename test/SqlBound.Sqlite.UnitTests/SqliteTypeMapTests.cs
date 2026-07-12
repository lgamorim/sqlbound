namespace SqlBound.Sqlite.UnitTests;

public sealed class SqliteTypeMapTests
{
    [Theory]
    [InlineData("BOOLEAN", "bool")]
    [InlineData("BOOL", "bool")]
    [InlineData("TINYINT", "byte")]
    [InlineData("SMALLINT", "short")]
    [InlineData("INT2", "short")]
    [InlineData("INT", "int")]
    [InlineData("INTEGER", "int")]
    [InlineData("MEDIUMINT", "int")]
    [InlineData("BIGINT", "long")]
    [InlineData("INT8", "long")]
    [InlineData("UNSIGNED BIG INT", "long")]
    [InlineData("REAL", "double")]
    [InlineData("DOUBLE", "double")]
    [InlineData("DOUBLE PRECISION", "double")]
    [InlineData("FLOAT", "double")]
    [InlineData("DECIMAL(10,5)", "decimal")]
    [InlineData("NUMERIC", "decimal")]
    [InlineData("MONEY", "decimal")]
    [InlineData("CHARACTER(20)", "string")]
    [InlineData("VARCHAR(255)", "string")]
    [InlineData("VARYING CHARACTER(255)", "string")]
    [InlineData("NCHAR(55)", "string")]
    [InlineData("NVARCHAR(100)", "string")]
    [InlineData("TEXT", "string")]
    [InlineData("CLOB", "string")]
    [InlineData("BLOB", "byte[]")]
    [InlineData("BINARY", "byte[]")]
    [InlineData("VARBINARY(50)", "byte[]")]
    [InlineData("GUID", "global::System.Guid")]
    [InlineData("UNIQUEIDENTIFIER", "global::System.Guid")]
    [InlineData("DATE", "global::System.DateTime")]
    [InlineData("DATETIME", "global::System.DateTime")]
    [InlineData("TIMESTAMP", "global::System.DateTime")]
    public void Should_MapToGeneratorTypeText_When_DeclaredTypeIsSupported(string declaredType, string expected)
    {
        var mapped = SqliteTypeMap.TryMap(declaredType, out var clrTypeText);

        Assert.True(mapped);
        Assert.Equal(expected, clrTypeText);
    }

    [Theory]
    [InlineData("SOMETYPE")]
    [InlineData("")]
    public void Should_RejectType_When_DeclaredTypeHasNoSupportedGetter(string declaredType)
    {
        var mapped = SqliteTypeMap.TryMap(declaredType, out var clrTypeText);

        Assert.False(mapped);
        Assert.Null(clrTypeText);
    }

    [Theory]
    [InlineData("integer", "int")]
    [InlineData("nvarchar(50)", "string")]
    public void Should_MapIgnoringCase_When_DeclaredTypeIsNotUpperCase(string declaredType, string expected)
    {
        var mapped = SqliteTypeMap.TryMap(declaredType, out var clrTypeText);

        Assert.True(mapped);
        Assert.Equal(expected, clrTypeText);
    }
}
