namespace SqlBound.Migrations.UnitTests;

public sealed class MigrationReverterTests
{
    [Fact]
    public void Should_ReturnNull_When_NothingApplied()
    {
        Assert.Null(MigrationReverter.Plan([Migration(1)], []));
    }

    [Fact]
    public void Should_ReturnMostRecentlyApplied_When_ReversibleMigrationsApplied()
    {
        var target = MigrationReverter.Plan([Migration(1), Migration(2)], [Applied(1), Applied(2)]);

        Assert.Equal(2, target!.Version);
    }

    [Fact]
    public void Should_ThrowInconsistency_When_TargetMigrationFilesAreMissing()
    {
        var exception = Assert.Throws<MigrationInconsistencyException>(
            () => MigrationReverter.Plan([], [Applied(1)]));

        Assert.Contains("missing", exception.Message);
    }

    [Fact]
    public void Should_ThrowInconsistency_When_TargetMigrationIsIrreversible()
    {
        var irreversible = new Migration(1, "m1", "-- up 1", DownScript: null, "checksum");

        var exception = Assert.Throws<MigrationInconsistencyException>(
            () => MigrationReverter.Plan([irreversible], [Applied(1)]));

        Assert.Contains("irreversible", exception.Message);
    }

    private static Migration Migration(long version, string checksum = "checksum") =>
        new(version, $"m{version}", $"-- up {version}", $"-- down {version}", checksum);

    private static AppliedMigration Applied(long version, string checksum = "checksum") =>
        new(version, $"m{version}", checksum, new DateTime(2026, 7, 12, 0, 0, 0), 1);
}
