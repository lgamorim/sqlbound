namespace SqlBound.Migrations;

/// <summary>A migration that the ledger records as already applied to the database.</summary>
/// <param name="Version">The migration's timestamp version.</param>
/// <param name="Name">The migration's name slug, as it was when applied.</param>
/// <param name="Checksum">The SHA-256 checksum of the up-script that was applied.</param>
/// <param name="AppliedOnUtc">When the migration was applied.</param>
/// <param name="ExecutionMs">How long the up-script took to run, in milliseconds.</param>
public sealed record AppliedMigration(
    long Version, string Name, string Checksum, DateTime AppliedOnUtc, long ExecutionMs);
