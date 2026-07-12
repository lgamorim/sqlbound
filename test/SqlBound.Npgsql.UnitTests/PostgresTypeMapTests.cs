namespace SqlBound.Npgsql.UnitTests;

public sealed class PostgresTypeMapTests
{
    [Theory]
    [InlineData("boolean", "bool")]
    [InlineData("smallint", "short")]
    [InlineData("integer", "int")]
    [InlineData("bigint", "long")]
    [InlineData("real", "float")]
    [InlineData("double precision", "double")]
    [InlineData("numeric", "decimal")]
    [InlineData("money", "decimal")]
    [InlineData("character", "string")]
    [InlineData("character varying", "string")]
    [InlineData("text", "string")]
    [InlineData("bytea", "byte[]")]
    [InlineData("uuid", "global::System.Guid")]
    [InlineData("date", "global::System.DateTime")]
    [InlineData("timestamp without time zone", "global::System.DateTime")]
    public void Should_MapToGeneratorTypeText_When_PostgresTypeIsSupported(string dataTypeName, string expected)
    {
        var mapped = PostgresTypeMap.TryMap(dataTypeName, out var clrTypeText);

        Assert.True(mapped);
        Assert.Equal(expected, clrTypeText);
    }

    [Theory]
    [InlineData("timestamp with time zone")]
    [InlineData("time without time zone")]
    [InlineData("time with time zone")]
    [InlineData("interval")]
    [InlineData("json")]
    [InlineData("jsonb")]
    [InlineData("inet")]
    [InlineData("integer[]")]
    [InlineData("sometype")]
    [InlineData("")]
    public void Should_RejectType_When_PostgresTypeHasNoSupportedGetter(string dataTypeName)
    {
        var mapped = PostgresTypeMap.TryMap(dataTypeName, out var clrTypeText);

        Assert.False(mapped);
        Assert.Null(clrTypeText);
    }

    [Theory]
    [InlineData("INTEGER", "int")]
    [InlineData("Character Varying", "string")]
    public void Should_MapIgnoringCase_When_PostgresTypeIsNotLowerCase(string dataTypeName, string expected)
    {
        var mapped = PostgresTypeMap.TryMap(dataTypeName, out var clrTypeText);

        Assert.True(mapped);
        Assert.Equal(expected, clrTypeText);
    }
}
