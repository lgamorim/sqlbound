namespace SqlBound.Migrations.UnitTests;

public sealed class MigrationPlanTests
{
    [Fact]
    public void Should_ReturnAllMigrationsAsPending_When_NothingApplied()
    {
        var plan = MigrationPlan.Create([Migration(1), Migration(2)], []);

        Assert.Equal([1, 2], plan.Pending.Select(migration => migration.Version));
    }

    [Fact]
    public void Should_ReturnOnlyUnappliedMigrations_When_SomeApplied()
    {
        var plan = MigrationPlan.Create(
            [Migration(1), Migration(2), Migration(3)],
            [Applied(1)]);

        Assert.Equal([2, 3], plan.Pending.Select(migration => migration.Version));
    }

    [Fact]
    public void Should_ReturnEmpty_When_AllApplied()
    {
        var plan = MigrationPlan.Create([Migration(1), Migration(2)], [Applied(1), Applied(2)]);

        Assert.Empty(plan.Pending);
    }

    [Fact]
    public void Should_TolerateAppliedMigrationWithNoFile_When_LedgerHasExtraVersion()
    {
        var plan = MigrationPlan.Create([Migration(2)], [Applied(1), Applied(2)]);

        Assert.Empty(plan.Pending);
    }

    [Fact]
    public void Should_ThrowInconsistency_When_AppliedMigrationChecksumDiffers()
    {
        var exception = Assert.Throws<MigrationInconsistencyException>(
            () => MigrationPlan.Create([Migration(1, "aaa")], [Applied(1, "bbb")]));

        Assert.Contains("checksum", exception.Message);
    }

    [Fact]
    public void Should_ThrowInconsistency_When_PendingMigrationIsOrderedBeforeAnApplied()
    {
        var exception = Assert.Throws<MigrationInconsistencyException>(
            () => MigrationPlan.Create([Migration(1), Migration(2)], [Applied(2)]));

        Assert.Contains("version order", exception.Message);
    }

    private static Migration Migration(long version, string checksum = "checksum") =>
        new(version, $"m{version}", $"-- up {version}", $"-- down {version}", checksum);

    private static AppliedMigration Applied(long version, string checksum = "checksum") =>
        new(version, $"m{version}", checksum, new DateTime(2026, 7, 12, 0, 0, 0), 1);
}
