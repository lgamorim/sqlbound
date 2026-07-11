namespace SqlBound.Cli;

/// <summary>
/// Reconciles the on-disk <c>.sqlbound/</c> directory with the snapshot set a prepare run
/// produced. <see cref="Compare"/> is the read-only half behind <c>prepare --check</c>;
/// <see cref="Apply"/> writes new and changed files and prunes orphans whose query no longer
/// exists. Only <c>query-*.json</c> files are managed; anything else in the directory is left
/// alone. Content comparison normalizes line endings, since git may check snapshots out as CRLF.
/// </summary>
internal static class SnapshotStore
{
    public const string DirectoryName = ".sqlbound";

    public static SnapshotDifference Compare(string snapshotDirectory, IReadOnlyDictionary<string, string> desired) =>
        Reconcile(snapshotDirectory, desired, apply: false);

    public static SnapshotDifference Apply(string snapshotDirectory, IReadOnlyDictionary<string, string> desired) =>
        Reconcile(snapshotDirectory, desired, apply: true);

    private static SnapshotDifference Reconcile(
        string snapshotDirectory, IReadOnlyDictionary<string, string> desired, bool apply)
    {
        var added = new List<string>();
        var changed = new List<string>();
        var unchanged = new List<string>();
        var orphaned = new List<string>();

        var existing = Directory.Exists(snapshotDirectory)
            ? Directory.GetFiles(snapshotDirectory, "query-*.json").Select(Path.GetFileName).Cast<string>().ToList()
            : [];

        foreach (var fileName in desired.Keys.OrderBy(name => name, StringComparer.Ordinal))
        {
            var path = Path.Combine(snapshotDirectory, fileName);
            if (!File.Exists(path))
            {
                added.Add(fileName);
            }
            else if (Normalize(File.ReadAllText(path)) == Normalize(desired[fileName]))
            {
                unchanged.Add(fileName);
                continue;
            }
            else
            {
                changed.Add(fileName);
            }

            if (apply)
            {
                Directory.CreateDirectory(snapshotDirectory);
                File.WriteAllText(path, desired[fileName]);
            }
        }

        foreach (var fileName in existing.OrderBy(name => name, StringComparer.Ordinal))
        {
            if (desired.ContainsKey(fileName))
            {
                continue;
            }

            orphaned.Add(fileName);
            if (apply)
            {
                File.Delete(Path.Combine(snapshotDirectory, fileName));
            }
        }

        return new SnapshotDifference(added, changed, unchanged, orphaned);
    }

    private static string Normalize(string content) => content.Replace("\r\n", "\n");
}

/// <summary>The outcome of reconciling desired snapshots against the committed directory.</summary>
internal sealed record SnapshotDifference(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Changed,
    IReadOnlyList<string> Unchanged,
    IReadOnlyList<string> Orphaned)
{
    public bool HasDrift => Added.Count > 0 || Changed.Count > 0 || Orphaned.Count > 0;
}
