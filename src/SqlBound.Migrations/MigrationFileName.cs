namespace SqlBound.Migrations;

/// <summary>
/// A parsed migration file name of the form <c>{version}_{name}.up.sql</c> or
/// <c>{version}_{name}.down.sql</c>, where <c>version</c> is an all-digit timestamp and
/// <c>name</c> is the migration's slug (which may itself contain underscores).
/// </summary>
internal readonly record struct MigrationFileName(long Version, string Name, MigrationDirection Direction)
{
    private const string UpSuffix = ".up.sql";
    private const string DownSuffix = ".down.sql";

    /// <summary>
    /// Whether a file name claims to be a migration (ends in <c>.up.sql</c>/<c>.down.sql</c>),
    /// regardless of whether its version and name are well-formed. Lets the pairing logic tell an
    /// unrelated file it should ignore from a malformed migration it should reject.
    /// </summary>
    public static bool HasMigrationSuffix(string fileName) =>
        fileName.EndsWith(UpSuffix, StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(DownSuffix, StringComparison.OrdinalIgnoreCase);

    public static bool TryParse(string fileName, out MigrationFileName result)
    {
        result = default;

        MigrationDirection direction;
        string stem;
        if (fileName.EndsWith(UpSuffix, StringComparison.OrdinalIgnoreCase))
        {
            direction = MigrationDirection.Up;
            stem = fileName[..^UpSuffix.Length];
        }
        else if (fileName.EndsWith(DownSuffix, StringComparison.OrdinalIgnoreCase))
        {
            direction = MigrationDirection.Down;
            stem = fileName[..^DownSuffix.Length];
        }
        else
        {
            return false;
        }

        var separator = stem.IndexOf('_');
        if (separator <= 0 || separator == stem.Length - 1)
        {
            return false;
        }

        var versionText = stem[..separator];
        if (!versionText.All(char.IsAsciiDigit) || !long.TryParse(versionText, out var version))
        {
            return false;
        }

        result = new MigrationFileName(version, stem[(separator + 1)..], direction);
        return true;
    }
}
