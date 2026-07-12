namespace SqlBound.Migrations.IntegrationTests;

/// <summary>
/// Exercises <see cref="MigrationDirectory.Load"/> against a real temp directory. These touch the
/// filesystem (hence integration, not unit), but need no Docker: the pairing and checksum logic is
/// unit-tested in isolation, so this only proves the I/O glue reads and orders files correctly.
/// </summary>
public sealed class MigrationDirectoryTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"sqlbound-migrations-{Guid.NewGuid():N}");

    public MigrationDirectoryTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Should_LoadReversibleMigration_When_UpAndDownPresent()
    {
        Write("20260712143000_create_items.up.sql", "CREATE TABLE items (id int);");
        Write("20260712143000_create_items.down.sql", "DROP TABLE items;");

        var migration = Assert.Single(MigrationDirectory.Load(_directory));

        Assert.Equal(20260712143000, migration.Version);
        Assert.Equal("create_items", migration.Name);
        Assert.Equal("CREATE TABLE items (id int);", migration.UpScript);
        Assert.Equal("DROP TABLE items;", migration.DownScript);
        Assert.True(migration.IsReversible);
        Assert.Equal(MigrationChecksum.Compute("CREATE TABLE items (id int);"), migration.Checksum);
    }

    [Fact]
    public void Should_LoadIrreversibleMigration_When_OnlyUpPresent()
    {
        Write("20260712143000_backfill.up.sql", "UPDATE items SET name = 'x';");

        var migration = Assert.Single(MigrationDirectory.Load(_directory));

        Assert.Null(migration.DownScript);
        Assert.False(migration.IsReversible);
    }

    [Fact]
    public void Should_OrderByVersion_When_MultipleMigrationsPresent()
    {
        Write("20260712150000_second.up.sql", "SELECT 2;");
        Write("20260712143000_first.up.sql", "SELECT 1;");

        var migrations = MigrationDirectory.Load(_directory);

        Assert.Equal([20260712143000, 20260712150000], migrations.Select(migration => migration.Version));
    }

    [Fact]
    public void Should_ReturnEmpty_When_DirectoryHasNoMigrations()
    {
        Write("README.md", "not a migration");

        Assert.Empty(MigrationDirectory.Load(_directory));
    }

    [Fact]
    public void Should_ThrowMigrationFormatException_When_DownHasNoMatchingUp()
    {
        Write("20260712143000_create_items.down.sql", "DROP TABLE items;");

        Assert.Throws<MigrationFormatException>(() => MigrationDirectory.Load(_directory));
    }

    [Fact]
    public void Should_ThrowDirectoryNotFoundException_When_DirectoryMissing()
    {
        Assert.Throws<DirectoryNotFoundException>(
            () => MigrationDirectory.Load(Path.Combine(_directory, "does-not-exist")));
    }

    private void Write(string fileName, string content) =>
        File.WriteAllText(Path.Combine(_directory, fileName), content);
}
