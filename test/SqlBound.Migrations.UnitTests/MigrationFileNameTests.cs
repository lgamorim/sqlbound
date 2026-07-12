namespace SqlBound.Migrations.UnitTests;

public sealed class MigrationFileNameTests
{
    [Fact]
    public void Should_ParseVersionNameAndDirection_When_UpFile()
    {
        var parsed = MigrationFileName.TryParse("20260712143000_create_items.up.sql", out var result);

        Assert.True(parsed);
        Assert.Equal(20260712143000, result.Version);
        Assert.Equal("create_items", result.Name);
        Assert.Equal(MigrationDirection.Up, result.Direction);
    }

    [Fact]
    public void Should_ParseDirectionDown_When_DownFile()
    {
        var parsed = MigrationFileName.TryParse("20260712143000_create_items.down.sql", out var result);

        Assert.True(parsed);
        Assert.Equal(MigrationDirection.Down, result.Direction);
    }

    [Fact]
    public void Should_PreserveUnderscoresInName_When_NameContainsUnderscores()
    {
        var parsed = MigrationFileName.TryParse("20260712143000_create_order_items.up.sql", out var result);

        Assert.True(parsed);
        Assert.Equal("create_order_items", result.Name);
    }

    [Fact]
    public void Should_ParseSuffixCaseInsensitively_When_SuffixIsUpperCase()
    {
        var parsed = MigrationFileName.TryParse("20260712143000_create_items.UP.SQL", out var result);

        Assert.True(parsed);
        Assert.Equal(MigrationDirection.Up, result.Direction);
    }

    [Fact]
    public void Should_ReturnFalse_When_NoDirectionSuffix()
    {
        Assert.False(MigrationFileName.TryParse("20260712143000_create_items.sql", out _));
    }

    [Fact]
    public void Should_ReturnFalse_When_VersionIsNotNumeric()
    {
        Assert.False(MigrationFileName.TryParse("v1_create_items.up.sql", out _));
    }

    [Fact]
    public void Should_ReturnFalse_When_NoUnderscoreSeparator()
    {
        Assert.False(MigrationFileName.TryParse("20260712143000.up.sql", out _));
    }

    [Fact]
    public void Should_ReturnFalse_When_NameIsEmpty()
    {
        Assert.False(MigrationFileName.TryParse("20260712143000_.up.sql", out _));
    }

    [Fact]
    public void Should_ReturnFalse_When_VersionIsEmpty()
    {
        Assert.False(MigrationFileName.TryParse("_create_items.up.sql", out _));
    }
}
