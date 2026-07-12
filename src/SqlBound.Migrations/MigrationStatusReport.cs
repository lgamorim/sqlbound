namespace SqlBound.Migrations;

/// <summary>
/// Classifies every migration — on disk, in the ledger, or both — into a <see cref="MigrationStatus"/>,
/// ordered by version. This is the read-only view behind <c>migrate status</c>; unlike
/// <see cref="MigrationPlan"/> it reports drift and missing migrations rather than throwing on them.
/// </summary>
internal static class MigrationStatusReport
{
    /// <summary>Classifies every known migration into its state relative to the ledger.</summary>
    /// <param name="migrations">The migrations on disk, as loaded from the directory.</param>
    /// <param name="applied">The migrations the ledger records as applied.</param>
    /// <returns>One status per migration known to either source, ascending by version.</returns>
    internal static IReadOnlyList<MigrationStatus> Build(
        IReadOnlyList<Migration> migrations, IReadOnlyList<AppliedMigration> applied)
    {
        var migrationsByVersion = migrations.ToDictionary(migration => migration.Version);
        var appliedByVersion = applied.ToDictionary(row => row.Version);

        var report = new List<MigrationStatus>();
        foreach (var version in migrationsByVersion.Keys.Union(appliedByVersion.Keys).OrderBy(version => version))
        {
            migrationsByVersion.TryGetValue(version, out var migration);
            appliedByVersion.TryGetValue(version, out var appliedRow);

            report.Add((migration, appliedRow) switch
            {
                (null, not null) =>
                    new MigrationStatus(version, appliedRow.Name, MigrationState.Missing, appliedRow.AppliedOnUtc),
                (not null, not null) =>
                    new MigrationStatus(
                        version,
                        migration.Name,
                        migration.Checksum == appliedRow.Checksum ? MigrationState.Applied : MigrationState.Drifted,
                        appliedRow.AppliedOnUtc),
                (not null, null) =>
                    new MigrationStatus(version, migration.Name, MigrationState.Pending, null),
                (null, null) =>
                    throw new InvalidOperationException($"Version {version} is in neither collection."),
            });
        }

        return report;
    }
}
