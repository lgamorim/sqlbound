using System.Security.Cryptography;
using System.Text;

namespace SqlBound.Migrations;

/// <summary>
/// Computes the checksum a migration's ledger row stores. The hash covers the up-script only, with
/// line endings normalized to <c>\n</c> first so a CRLF checkout does not invalidate a checksum
/// written from an LF working tree (the same normalization the snapshot store applies).
/// </summary>
internal static class MigrationChecksum
{
    public static string Compute(string upScript)
    {
        var normalized = upScript.Replace("\r\n", "\n");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
