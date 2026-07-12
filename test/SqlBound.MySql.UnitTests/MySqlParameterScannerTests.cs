namespace SqlBound.MySql.UnitTests;

public sealed class MySqlParameterScannerTests
{
    [Fact]
    public void Should_ReturnEmpty_When_CommandTextHasNoParameters()
    {
        Assert.Empty(MySqlParameterScanner.ExtractNames("SELECT id FROM items"));
    }

    [Fact]
    public void Should_ReturnEmpty_When_CommandTextIsEmpty()
    {
        Assert.Empty(MySqlParameterScanner.ExtractNames(""));
    }

    [Fact]
    public void Should_ReturnNamesInOrderOfFirstAppearance_When_CommandUsesMultipleParameters()
    {
        var names = MySqlParameterScanner.ExtractNames(
            "SELECT id FROM items WHERE id = @id AND name = @name AND price > @minPrice");

        Assert.Equal(["id", "name", "minPrice"], names);
    }

    [Fact]
    public void Should_ReturnOneEntry_When_TheSameParameterIsUsedMultipleTimes()
    {
        var names = MySqlParameterScanner.ExtractNames("SELECT id FROM items WHERE id = @p OR name = @p");

        Assert.Equal(["p"], names);
    }

    [Fact]
    public void Should_IgnoreAtSignInsideASingleQuotedStringLiteral()
    {
        var names = MySqlParameterScanner.ExtractNames(
            "SELECT id FROM items WHERE email = 'user@example.com' AND id = @id");

        Assert.Equal(["id"], names);
    }

    [Fact]
    public void Should_IgnoreAtSignInsideADoubleQuotedStringLiteral()
    {
        var names = MySqlParameterScanner.ExtractNames(
            """SELECT id FROM items WHERE email = "user@example.com" AND id = @id""");

        Assert.Equal(["id"], names);
    }

    [Fact]
    public void Should_IgnoreAtSignInsideABacktickQuotedIdentifier()
    {
        var names = MySqlParameterScanner.ExtractNames("SELECT `@col` FROM items WHERE id = @id");

        Assert.Equal(["id"], names);
    }

    [Fact]
    public void Should_IgnoreAtSignInsideADoubledQuoteEscapedStringLiteral()
    {
        var names = MySqlParameterScanner.ExtractNames(
            "SELECT id FROM items WHERE name = 'it''s @fake' AND id = @id");

        Assert.Equal(["id"], names);
    }

    [Fact]
    public void Should_IgnoreAtSignInsideABackslashEscapedStringLiteral()
    {
        var names = MySqlParameterScanner.ExtractNames(
            "SELECT id FROM items WHERE name = 'a\\'@fake' AND id = @id");

        Assert.Equal(["id"], names);
    }

    [Fact]
    public void Should_IgnoreAtSignInsideADoubleDashLineComment()
    {
        var names = MySqlParameterScanner.ExtractNames(
            "SELECT id FROM items -- @fake\nWHERE id = @id");

        Assert.Equal(["id"], names);
    }

    [Fact]
    public void Should_IgnoreAtSignInsideAHashLineComment()
    {
        var names = MySqlParameterScanner.ExtractNames(
            "SELECT id FROM items # @fake\nWHERE id = @id");

        Assert.Equal(["id"], names);
    }

    [Fact]
    public void Should_IgnoreAtSignInsideABlockComment()
    {
        var names = MySqlParameterScanner.ExtractNames(
            "SELECT id FROM items /* @fake */ WHERE id = @id");

        Assert.Equal(["id"], names);
    }
}
