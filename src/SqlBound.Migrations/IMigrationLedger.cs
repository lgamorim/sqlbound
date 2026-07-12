using System.Data.Common;

namespace SqlBound.Migrations;

/// <summary>
/// The per-provider migration history table (<c>_sqlbound_migrations</c>): the durable record of
/// which migrations have been applied. M13 defines the read side; the write side arrives with
/// <c>migrate run</c>. Implementations own only the SQL — they never open or close the connection.
/// </summary>
public interface IMigrationLedger
{
    /// <summary>Creates the ledger table if it does not already exist. Idempotent.</summary>
    /// <param name="connection">An open connection to the target database.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task EnsureCreatedAsync(DbConnection connection, CancellationToken cancellationToken);

    /// <summary>Reads the applied migrations from the ledger, ordered ascending by version.</summary>
    /// <param name="connection">An open connection to the target database.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The applied migrations; empty when none have been applied.</returns>
    Task<IReadOnlyList<AppliedMigration>> GetAppliedAsync(DbConnection connection, CancellationToken cancellationToken);
}
