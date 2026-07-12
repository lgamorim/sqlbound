namespace SqlBound.Migrations;

/// <summary>
/// Thrown when the migrations directory is not a well-formed set: a malformed file name, a
/// duplicated version, a rollback script with no matching forward script, or an up/down pair whose
/// names disagree.
/// </summary>
public sealed class MigrationFormatException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="MigrationFormatException"/> class.</summary>
    /// <param name="message">A description of what makes the migration set malformed.</param>
    public MigrationFormatException(string message)
        : base(message)
    {
    }
}
