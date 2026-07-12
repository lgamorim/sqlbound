namespace SqlBound.Migrations;

/// <summary>
/// Pairs the raw file names of a migrations directory into ordered up/down pairs, validating the
/// set as it goes: file names that do not look like migrations are ignored, but a malformed
/// migration file, a duplicated version, an up/down pair whose names disagree, or a rollback with
/// no forward script is rejected with a <see cref="MigrationFormatException"/>.
/// </summary>
internal static class MigrationPairing
{
    public static IReadOnlyList<MigrationFilePair> Pair(IEnumerable<string> fileNames)
    {
        var ups = new Dictionary<long, (string Name, string FileName)>();
        var downs = new Dictionary<long, (string Name, string FileName)>();

        foreach (var fileName in fileNames)
        {
            if (!MigrationFileName.HasMigrationSuffix(fileName))
            {
                continue;
            }

            if (!MigrationFileName.TryParse(fileName, out var parsed))
            {
                throw new MigrationFormatException(
                    $"'{fileName}' is not a valid migration file name; expected {{version}}_{{name}}.up.sql or .down.sql.");
            }

            var target = parsed.Direction == MigrationDirection.Up ? ups : downs;
            if (!target.TryAdd(parsed.Version, (parsed.Name, fileName)))
            {
                throw new MigrationFormatException(
                    $"version {parsed.Version} is used by more than one migration '{parsed.Direction.ToString().ToLowerInvariant()}' file.");
            }
        }

        var pairs = new List<MigrationFilePair>(ups.Count);
        foreach (var (version, up) in ups)
        {
            string? downFileName = null;
            if (downs.TryGetValue(version, out var down))
            {
                if (!string.Equals(down.Name, up.Name, StringComparison.Ordinal))
                {
                    throw new MigrationFormatException(
                        $"version {version} has mismatched names: '{up.FileName}' and '{down.FileName}'.");
                }

                downFileName = down.FileName;
                downs.Remove(version);
            }

            pairs.Add(new MigrationFilePair(version, up.Name, up.FileName, downFileName));
        }

        if (downs.Count > 0)
        {
            var orphan = downs.Values.First();
            throw new MigrationFormatException(
                $"'{orphan.FileName}' has no matching .up.sql; a rollback script cannot exist on its own.");
        }

        pairs.Sort((left, right) => left.Version.CompareTo(right.Version));
        return pairs;
    }
}

/// <summary>An up-script file and its optional down-script file, resolved to one migration version.</summary>
internal sealed record MigrationFilePair(long Version, string Name, string UpFileName, string? DownFileName);
