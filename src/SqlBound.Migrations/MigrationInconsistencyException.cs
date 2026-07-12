namespace SqlBound.Migrations;

/// <summary>
/// Thrown when the migrations directory is inconsistent with the applied history: an applied
/// migration's up-script has been edited (checksum drift), a not-yet-applied migration is ordered
/// before one that is already applied, or the migration to revert is irreversible or missing. This
/// is distinct from <see cref="MigrationFormatException"/>, which is about the directory being
/// malformed in isolation, before any comparison with the ledger.
/// </summary>
public sealed class MigrationInconsistencyException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="MigrationInconsistencyException"/> class.</summary>
    /// <param name="message">A description of how the directory and the applied history disagree.</param>
    public MigrationInconsistencyException(string message)
        : base(message)
    {
    }
}
