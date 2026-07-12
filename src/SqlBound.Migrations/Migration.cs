namespace SqlBound.Migrations;

/// <summary>
/// A migration loaded from disk: its version and name, the forward script and its checksum, and
/// the rollback script when the migration is reversible.
/// </summary>
/// <param name="Version">The migration's timestamp version (<c>yyyyMMddHHmmss</c>).</param>
/// <param name="Name">The migration's name slug.</param>
/// <param name="UpScript">The forward script that <c>migrate run</c> applies.</param>
/// <param name="DownScript">The rollback script, or <see langword="null"/> when the migration is irreversible.</param>
/// <param name="Checksum">The SHA-256 checksum of the up-script, as stored in the ledger.</param>
public sealed record Migration(long Version, string Name, string UpScript, string? DownScript, string Checksum)
{
    /// <summary>Whether the migration ships a rollback script and can therefore be reverted.</summary>
    public bool IsReversible => DownScript is not null;
}
