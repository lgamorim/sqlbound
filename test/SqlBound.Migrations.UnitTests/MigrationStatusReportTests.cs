namespace SqlBound.Migrations.UnitTests;

public sealed class MigrationStatusReportTests
{
    [Fact]
    public void Should_ClassifyAppliedAndPending_When_SomeMigrationsApplied()
    {
        var report = MigrationStatusReport.Build([Migration(1), Migration(2)], [Applied(1)]);

        Assert.Equal(
            [
                new MigrationStatus(1, "m1", MigrationState.Applied, new DateTime(2026, 7, 12, 0, 0, 0)),
                new MigrationStatus(2, "m2", MigrationState.Pending, null),
            ],
            report);
    }

    [Fact]
    public void Should_ClassifyDrifted_When_ChecksumDiffers()
    {
        var report = MigrationStatusReport.Build([Migration(1, "aaa")], [Applied(1, "bbb")]);

        Assert.Equal(MigrationState.Drifted, Assert.Single(report).State);
    }

    [Fact]
    public void Should_ClassifyMissing_When_AppliedMigrationHasNoFile()
    {
        var report = MigrationStatusReport.Build([], [Applied(1)]);

        Assert.Equal(MigrationState.Missing, Assert.Single(report).State);
    }

    [Fact]
    public void Should_OrderByVersion_When_MigrationsAndLedgerInterleave()
    {
        var report = MigrationStatusReport.Build([Migration(3), Migration(1)], [Applied(1), Applied(2)]);

        Assert.Equal([1, 2, 3], report.Select(status => status.Version));
    }

    private static Migration Migration(long version, string checksum = "checksum") =>
        new(version, $"m{version}", $"-- up {version}", $"-- down {version}", checksum);

    private static AppliedMigration Applied(long version, string checksum = "checksum") =>
        new(version, $"m{version}", checksum, new DateTime(2026, 7, 12, 0, 0, 0), 1);
}
