using System.Data.Common;
using System.Diagnostics;

namespace SqlBound.Migrations;

/// <summary>
/// Applies and reverts migrations against a database. The orchestration is provider-neutral — a
/// migration script is executed as an ordinary <see cref="DbCommand"/> and the ledger is written
/// through <see cref="IMigrationLedger"/> — so every provider shares this engine. Each migration is
/// applied in its own transaction together with its ledger row: a failure rolls back that one
/// migration and stops, leaving every earlier migration applied.
/// </summary>
public static class MigrationRunner
{
    /// <summary>Applies every pending migration, in version order.</summary>
    /// <param name="connection">An open connection to the target database.</param>
    /// <param name="ledger">The migration ledger for the target provider.</param>
    /// <param name="migrations">The migrations on disk, as loaded from the directory.</param>
    /// <param name="timeProvider">Supplies the <c>applied_on_utc</c> timestamp.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The migrations applied by this run, in order; empty when already up to date.</returns>
    /// <exception cref="MigrationInconsistencyException">The directory disagrees with the applied history.</exception>
    /// <exception cref="MigrationExecutionException">A migration's up-script failed to execute.</exception>
    public static async Task<IReadOnlyList<AppliedMigration>> RunAsync(
        DbConnection connection,
        IMigrationLedger ledger,
        IReadOnlyList<Migration> migrations,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        await ledger.EnsureCreatedAsync(connection, cancellationToken).ConfigureAwait(false);
        var applied = await ledger.GetAppliedAsync(connection, cancellationToken).ConfigureAwait(false);
        var plan = MigrationPlan.Create(migrations, applied);

        var appliedNow = new List<AppliedMigration>();
        foreach (var migration in plan.Pending)
        {
            var record = await ApplyAsync(connection, ledger, migration, timeProvider, cancellationToken)
                .ConfigureAwait(false);
            appliedNow.Add(record);
        }

        return appliedNow;
    }

    /// <summary>Reverts the most recently applied migration, running its down-script.</summary>
    /// <param name="connection">An open connection to the target database.</param>
    /// <param name="ledger">The migration ledger for the target provider.</param>
    /// <param name="migrations">The migrations on disk, as loaded from the directory.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The reverted migration, or <see langword="null"/> when nothing was applied.</returns>
    /// <exception cref="MigrationInconsistencyException">The migration to revert is missing or irreversible.</exception>
    /// <exception cref="MigrationExecutionException">The down-script failed to execute.</exception>
    public static async Task<Migration?> RevertAsync(
        DbConnection connection,
        IMigrationLedger ledger,
        IReadOnlyList<Migration> migrations,
        CancellationToken cancellationToken)
    {
        await ledger.EnsureCreatedAsync(connection, cancellationToken).ConfigureAwait(false);
        var applied = await ledger.GetAppliedAsync(connection, cancellationToken).ConfigureAwait(false);
        var target = MigrationReverter.Plan(migrations, applied);
        if (target is null)
        {
            return null;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // MigrationReverter.Plan guarantees a reversible target, so DownScript is non-null here.
            await ExecuteScriptAsync(connection, transaction, target.DownScript!, cancellationToken).ConfigureAwait(false);
            await ledger.RemoveAsync(connection, transaction, target.Version, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return target;
        }
        catch (DbException exception)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new MigrationExecutionException(target.Version, target.Name, exception);
        }
    }

    /// <summary>Reports each migration's state relative to the ledger, without changing anything.</summary>
    /// <param name="connection">An open connection to the target database.</param>
    /// <param name="ledger">The migration ledger for the target provider.</param>
    /// <param name="migrations">The migrations on disk, as loaded from the directory.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>One status per migration known to disk or the ledger, ascending by version.</returns>
    public static async Task<IReadOnlyList<MigrationStatus>> StatusAsync(
        DbConnection connection,
        IMigrationLedger ledger,
        IReadOnlyList<Migration> migrations,
        CancellationToken cancellationToken)
    {
        await ledger.EnsureCreatedAsync(connection, cancellationToken).ConfigureAwait(false);
        var applied = await ledger.GetAppliedAsync(connection, cancellationToken).ConfigureAwait(false);
        return MigrationStatusReport.Build(migrations, applied);
    }

    private static async Task<AppliedMigration> ApplyAsync(
        DbConnection connection,
        IMigrationLedger ledger,
        Migration migration,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            await ExecuteScriptAsync(connection, transaction, migration.UpScript, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            var record = new AppliedMigration(
                migration.Version,
                migration.Name,
                migration.Checksum,
                timeProvider.GetUtcNow().UtcDateTime,
                stopwatch.ElapsedMilliseconds);
            await ledger.RecordAppliedAsync(connection, transaction, record, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return record;
        }
        catch (DbException exception)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new MigrationExecutionException(migration.Version, migration.Name, exception);
        }
    }

    private static async Task ExecuteScriptAsync(
        DbConnection connection, DbTransaction transaction, string script, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = script;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
