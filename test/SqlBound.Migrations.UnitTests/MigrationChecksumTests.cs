namespace SqlBound.Migrations.UnitTests;

public sealed class MigrationChecksumTests
{
    [Fact]
    public void Should_ReturnLowercaseHexOf64Characters_When_ScriptIsHashed()
    {
        var checksum = MigrationChecksum.Compute("SELECT 1");

        Assert.Equal(64, checksum.Length);
        Assert.All(checksum, character => Assert.Contains(character, "0123456789abcdef"));
    }

    [Fact]
    public void Should_BeDeterministic_When_SameScriptIsHashedTwice()
    {
        Assert.Equal(
            MigrationChecksum.Compute("CREATE TABLE items (id int);"),
            MigrationChecksum.Compute("CREATE TABLE items (id int);"));
    }

    [Fact]
    public void Should_ProduceDifferentHashes_When_ScriptsDiffer()
    {
        Assert.NotEqual(
            MigrationChecksum.Compute("SELECT 1"),
            MigrationChecksum.Compute("SELECT 2"));
    }

    [Fact]
    public void Should_IgnoreLineEndingStyle_When_ScriptDiffersOnlyByCarriageReturns()
    {
        Assert.Equal(
            MigrationChecksum.Compute("line one\nline two\n"),
            MigrationChecksum.Compute("line one\r\nline two\r\n"));
    }
}
