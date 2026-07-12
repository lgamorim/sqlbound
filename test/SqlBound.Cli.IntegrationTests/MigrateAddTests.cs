namespace SqlBound.Cli.IntegrationTests;

/// <summary>
/// Exercises <see cref="MigrationScaffolder.Create"/> against a real temp directory with a fixed
/// clock, so the timestamped file names are deterministic. No Docker: scaffolding is pure file I/O.
/// </summary>
public sealed class MigrateAddTests : IDisposable
{
    private static readonly TimeProvider FixedClock =
        new FixedTimeProvider(new DateTimeOffset(2026, 7, 12, 14, 30, 0, TimeSpan.Zero));

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"sqlbound-add-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Should_CreateUpAndDownFiles_When_Reversible()
    {
        var created = MigrationScaffolder.Create(_directory, "create items", reversible: true, FixedClock);

        Assert.Equal(
            [
                Path.Combine(_directory, "20260712143000_create_items.up.sql"),
                Path.Combine(_directory, "20260712143000_create_items.down.sql"),
            ],
            created);
        Assert.All(created, path => Assert.True(File.Exists(path)));
        Assert.Contains(
            "-- Migration: 20260712143000_create_items (up)",
            File.ReadAllText(created[0]));
    }

    [Fact]
    public void Should_CreateOnlyUpFile_When_Irreversible()
    {
        var created = MigrationScaffolder.Create(_directory, "backfill", reversible: false, FixedClock);

        Assert.Single(created);
        Assert.EndsWith(".up.sql", created[0]);
        Assert.False(File.Exists(Path.Combine(_directory, "20260712143000_backfill.down.sql")));
    }

    [Fact]
    public void Should_CreateMigrationsDirectory_When_ItDoesNotExist()
    {
        Assert.False(Directory.Exists(_directory));

        MigrationScaffolder.Create(_directory, "init", reversible: true, FixedClock);

        Assert.True(Directory.Exists(_directory));
    }

    [Fact]
    public void Should_ThrowArgumentException_When_NameHasNoUsableCharacters()
    {
        Assert.Throws<ArgumentException>(
            () => MigrationScaffolder.Create(_directory, "!!!", reversible: true, FixedClock));
    }

    [Fact]
    public void Should_ThrowInvalidOperationException_When_MigrationAlreadyExistsForThatSecond()
    {
        MigrationScaffolder.Create(_directory, "create items", reversible: true, FixedClock);

        Assert.Throws<InvalidOperationException>(
            () => MigrationScaffolder.Create(_directory, "create items", reversible: true, FixedClock));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
