namespace SqlBound.Migrations.UnitTests;

public sealed class MigrationPairingTests
{
    [Fact]
    public void Should_ReturnEmpty_When_NoFiles()
    {
        Assert.Empty(MigrationPairing.Pair([]));
    }

    [Fact]
    public void Should_PairUpAndDown_When_BothPresent()
    {
        var pairs = MigrationPairing.Pair(
            ["20260712143000_create_items.up.sql", "20260712143000_create_items.down.sql"]);

        var pair = Assert.Single(pairs);
        Assert.Equal(20260712143000, pair.Version);
        Assert.Equal("create_items", pair.Name);
        Assert.Equal("20260712143000_create_items.up.sql", pair.UpFileName);
        Assert.Equal("20260712143000_create_items.down.sql", pair.DownFileName);
    }

    [Fact]
    public void Should_LeaveDownNull_When_OnlyUpPresent()
    {
        var pairs = MigrationPairing.Pair(["20260712143000_create_items.up.sql"]);

        var pair = Assert.Single(pairs);
        Assert.Null(pair.DownFileName);
    }

    [Fact]
    public void Should_OrderByVersionAscending_When_FilesAreOutOfOrder()
    {
        var pairs = MigrationPairing.Pair(
            ["20260712150000_second.up.sql", "20260712143000_first.up.sql"]);

        Assert.Equal([20260712143000, 20260712150000], pairs.Select(pair => pair.Version));
    }

    [Fact]
    public void Should_IgnoreNonMigrationFiles_When_DirectoryHasOtherContent()
    {
        var pairs = MigrationPairing.Pair(
            ["README.md", ".gitkeep", "20260712143000_create_items.up.sql"]);

        Assert.Single(pairs);
    }

    [Fact]
    public void Should_Throw_When_VersionIsDuplicated()
    {
        var exception = Assert.Throws<MigrationFormatException>(() => MigrationPairing.Pair(
            ["20260712143000_first.up.sql", "20260712143000_second.up.sql"]));

        Assert.Contains("20260712143000", exception.Message);
    }

    [Fact]
    public void Should_Throw_When_DownHasNoMatchingUp()
    {
        var exception = Assert.Throws<MigrationFormatException>(() => MigrationPairing.Pair(
            ["20260712143000_create_items.down.sql"]));

        Assert.Contains("create_items", exception.Message);
    }

    [Fact]
    public void Should_Throw_When_UpAndDownVersionMatchesButNameDiffers()
    {
        Assert.Throws<MigrationFormatException>(() => MigrationPairing.Pair(
            ["20260712143000_create_items.up.sql", "20260712143000_create_widgets.down.sql"]));
    }

    [Fact]
    public void Should_Throw_When_MigrationFileHasMalformedVersion()
    {
        Assert.Throws<MigrationFormatException>(() => MigrationPairing.Pair(["v1_create_items.up.sql"]));
    }
}
