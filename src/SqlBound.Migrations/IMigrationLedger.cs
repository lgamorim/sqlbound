using System.Data.Common;

namespace SqlBound.Migrations;

/// <summary>
/// The per-provider migration history table (<c>_sqlbound_migrations</c>): the durable record of
/// which migrations have been applied. Implementations own only the SQL — they never open or close
/// the connection. The write methods take the ambient transaction so the ledger row commits
/// atomically with the migration's own script; the read methods run outside any transaction.
/// </summary>
public interface IMigrationLedger
{
    /// <summary>
    /// Whether the provider applies schema changes within a transaction. When <see langword="false"/>,
    /// <c>migrate run</c> applies each migration without wrapping it in a transaction, because the
    /// provider commits DDL implicitly (as MySQL does) — so a mid-migration failure cannot be
    /// rolled back. <see langword="true"/> for SQL Server, PostgreSQL, and SQLite.
    /// </summary>
    bool SupportsTransactionalDdl { get; }

    /// <summary>Creates the ledger table if it does not already exist. Idempotent.</summary>
    /// <param name="connection">An open connection to the target database.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task EnsureCreatedAsync(DbConnection connection, CancellationToken cancellationToken);

    /// <summary>Reads the applied migrations from the ledger, ordered ascending by version.</summary>
    /// <param name="connection">An open connection to the target database.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The applied migrations; empty when none have been applied.</returns>
    Task<IReadOnlyList<AppliedMigration>> GetAppliedAsync(DbConnection connection, CancellationToken cancellationToken);

    /// <summary>Records a migration as applied, on the given transaction.</summary>
    /// <param name="connection">An open connection to the target database.</param>
    /// <param name="transaction">The transaction the migration's up-script is running on, or <see langword="null"/>.</param>
    /// <param name="migration">The migration to record.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RecordAppliedAsync(
        DbConnection connection, DbTransaction? transaction, AppliedMigration migration, CancellationToken cancellationToken);

    /// <summary>Removes a migration's row from the ledger, on the given transaction.</summary>
    /// <param name="connection">An open connection to the target database.</param>
    /// <param name="transaction">The transaction the migration's down-script is running on, or <see langword="null"/>.</param>
    /// <param name="version">The version of the migration to remove.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RemoveAsync(
        DbConnection connection, DbTransaction? transaction, long version, CancellationToken cancellationToken);
}
