namespace SqlBound.Migrations;

/// <summary>
/// The ordered set of migrations <c>migrate run</c> would apply, computed from the directory and
/// the ledger. Creating the plan is also where the run's safety checks live: it refuses to proceed
/// if an already-applied migration has been edited, or if a pending migration is ordered before one
/// that is already applied.
/// </summary>
/// <param name="Pending">The migrations to apply, ascending by version.</param>
public sealed record MigrationPlan(IReadOnlyList<Migration> Pending)
{
    /// <summary>Computes the plan, validating the directory against the applied history.</summary>
    /// <param name="migrations">The migrations on disk, as loaded from the directory.</param>
    /// <param name="applied">The migrations the ledger records as applied.</param>
    /// <exception cref="MigrationInconsistencyException">
    /// An applied migration's checksum has drifted, or a pending migration is out of order.
    /// </exception>
    public static MigrationPlan Create(
        IReadOnlyList<Migration> migrations, IReadOnlyList<AppliedMigration> applied)
    {
        var appliedByVersion = applied.ToDictionary(row => row.Version);

        foreach (var migration in migrations)
        {
            if (appliedByVersion.TryGetValue(migration.Version, out var appliedRow)
                && migration.Checksum != appliedRow.Checksum)
            {
                throw new MigrationInconsistencyException(
                    $"migration {migration.Version}_{migration.Name} has been modified since it was applied "
                    + "(checksum mismatch); an applied migration must not be edited.");
            }
        }

        var pending = migrations
            .Where(migration => !appliedByVersion.ContainsKey(migration.Version))
            .OrderBy(migration => migration.Version)
            .ToList();

        if (applied.Count > 0 && pending.Count > 0)
        {
            var highestApplied = applied.Max(row => row.Version);
            var outOfOrder = pending.FirstOrDefault(migration => migration.Version < highestApplied);
            if (outOfOrder is not null)
            {
                throw new MigrationInconsistencyException(
                    $"migration {outOfOrder.Version}_{outOfOrder.Name} is ordered before the already-applied "
                    + $"migration {highestApplied}; migrations must be applied in version order.");
            }
        }

        return new MigrationPlan(pending);
    }
}
