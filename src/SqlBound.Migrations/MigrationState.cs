namespace SqlBound.Migrations;

/// <summary>A migration's state relative to the ledger, as reported by <c>migrate status</c>.</summary>
public enum MigrationState
{
    /// <summary>Present on disk and applied; the checksums agree.</summary>
    Applied,

    /// <summary>Present on disk but not yet applied.</summary>
    Pending,

    /// <summary>Applied, but the on-disk up-script no longer matches the applied checksum.</summary>
    Drifted,

    /// <summary>Recorded as applied in the ledger, but no longer present on disk.</summary>
    Missing,
}
