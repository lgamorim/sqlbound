using System.Security.Cryptography;
using System.Text;

namespace SqlBound.Cli;

/// <summary>
/// The prepare-side twin of the analyzer's <c>SqlBound.Generators.SnapshotKey</c>: lowercase hex
/// SHA-256 of the raw UTF-8 command text. The two implementations cannot share source (the same
/// type in two internals-visible assemblies is ambiguous to their shared tests), so parity is
/// enforced by test instead — if they ever diverge, prepare and verification silently split.
/// </summary>
internal static class SnapshotKey
{
    public static string Compute(string commandText) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(commandText))).ToLowerInvariant();
}
