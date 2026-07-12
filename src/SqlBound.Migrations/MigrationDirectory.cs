namespace SqlBound.Migrations;

/// <summary>
/// Loads the migrations directory into an ordered list of <see cref="Migration"/>. This is the I/O
/// boundary of the migration model: it enumerates and reads the files, delegating validation and
/// ordering to <see cref="MigrationPairing"/> and checksumming to <see cref="MigrationChecksum"/>.
/// </summary>
public static class MigrationDirectory
{
    /// <summary>Loads every migration in <paramref name="path"/>, ordered by version.</summary>
    /// <param name="path">The migrations directory.</param>
    /// <returns>The migrations, ascending by version; empty when the directory holds none.</returns>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    /// <exception cref="MigrationFormatException">The directory is not a well-formed migration set.</exception>
    public static IReadOnlyList<Migration> Load(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"The migrations directory '{path}' does not exist.");
        }

        var fileNames = Directory.EnumerateFiles(path).Select(Path.GetFileName).Cast<string>();
        var pairs = MigrationPairing.Pair(fileNames);

        var migrations = new List<Migration>(pairs.Count);
        foreach (var pair in pairs)
        {
            var upScript = File.ReadAllText(Path.Combine(path, pair.UpFileName));
            var downScript = pair.DownFileName is null
                ? null
                : File.ReadAllText(Path.Combine(path, pair.DownFileName));
            migrations.Add(new Migration(
                pair.Version, pair.Name, upScript, downScript, MigrationChecksum.Compute(upScript)));
        }

        return migrations;
    }
}
