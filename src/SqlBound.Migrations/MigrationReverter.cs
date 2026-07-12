namespace SqlBound.Migrations;

/// <summary>
/// Selects the migration <c>migrate revert</c> would roll back: the most recently applied one. The
/// selection fails if that migration's files are gone or it ships no down script, so the caller
/// never runs a rollback it cannot honor.
/// </summary>
public static class MigrationReverter
{
    /// <summary>Chooses the migration to revert.</summary>
    /// <param name="migrations">The migrations on disk, as loaded from the directory.</param>
    /// <param name="applied">The migrations the ledger records as applied.</param>
    /// <returns>The migration to revert, or <see langword="null"/> when nothing is applied.</returns>
    /// <exception cref="MigrationInconsistencyException">
    /// The most recently applied migration is missing from disk or is irreversible.
    /// </exception>
    public static Migration? Plan(IReadOnlyList<Migration> migrations, IReadOnlyList<AppliedMigration> applied)
    {
        if (applied.Count == 0)
        {
            return null;
        }

        var target = applied.OrderByDescending(row => row.Version).First();
        var migration = migrations.FirstOrDefault(candidate => candidate.Version == target.Version);
        if (migration is null)
        {
            throw new MigrationInconsistencyException(
                $"cannot revert {target.Version}_{target.Name}: its migration files are missing.");
        }

        if (!migration.IsReversible)
        {
            throw new MigrationInconsistencyException(
                $"cannot revert {migration.Version}_{migration.Name}: it is irreversible (no down script).");
        }

        return migration;
    }
}
