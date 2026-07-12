namespace SqlBound.Migrations;

/// <summary>One migration's state relative to the ledger.</summary>
/// <param name="Version">The migration's timestamp version.</param>
/// <param name="Name">The migration's name slug.</param>
/// <param name="State">The migration's state relative to the applied history.</param>
/// <param name="AppliedOnUtc">When the migration was applied, or <see langword="null"/> if it is pending.</param>
public sealed record MigrationStatus(long Version, string Name, MigrationState State, DateTime? AppliedOnUtc);
