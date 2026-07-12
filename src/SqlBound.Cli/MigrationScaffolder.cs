using System.Globalization;
using System.Text;

namespace SqlBound.Cli;

/// <summary>
/// Creates the file pair for <c>migrate add</c>: a <c>{version}_{name}.up.sql</c> forward script
/// and, unless the migration is irreversible, its <c>.down.sql</c> rollback. The version is the
/// current UTC time as <c>yyyyMMddHHmmss</c> (see ADR 0006); <see cref="TimeProvider"/> supplies it
/// so the scaffolding is deterministic under test.
/// </summary>
internal static class MigrationScaffolder
{
    public static IReadOnlyList<string> Create(
        string migrationsDirectory, string name, bool reversible, TimeProvider timeProvider)
    {
        var slug = Slugify(name);
        if (slug.Length == 0)
        {
            throw new ArgumentException(
                $"'{name}' has no letters or digits to form a migration name.", nameof(name));
        }

        var version = long.Parse(
            timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture);

        Directory.CreateDirectory(migrationsDirectory);

        var created = new List<string>(2);
        created.Add(WriteScript(migrationsDirectory, version, slug, "up", "the forward SQL for this migration"));
        if (reversible)
        {
            created.Add(WriteScript(migrationsDirectory, version, slug, "down", "the SQL that reverses this migration"));
        }

        return created;
    }

    internal static string Slugify(string name)
    {
        var builder = new StringBuilder(name.Length);
        var pendingSeparator = false;
        foreach (var character in name)
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                if (pendingSeparator)
                {
                    builder.Append('_');
                    pendingSeparator = false;
                }

                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0)
            {
                pendingSeparator = true;
            }
        }

        return builder.ToString();
    }

    private static string WriteScript(
        string migrationsDirectory, long version, string slug, string direction, string guidance)
    {
        var path = Path.Combine(migrationsDirectory, $"{version}_{slug}.{direction}.sql");
        if (File.Exists(path))
        {
            throw new InvalidOperationException(
                $"'{path}' already exists; a migration for this second was just created — try again.");
        }

        File.WriteAllText(
            path,
            $"""
            -- Migration: {version}_{slug} ({direction})
            -- Write {guidance} below.

            """);
        return path;
    }
}
