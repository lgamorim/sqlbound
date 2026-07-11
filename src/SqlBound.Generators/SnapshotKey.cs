using System.Security.Cryptography;
using System.Text;

namespace SqlBound.Generators;

/// <summary>
/// Derives the key that pairs a query with its <c>.sqlbound/</c> snapshot file: the lowercase hex
/// SHA-256 of the raw UTF-8 command text. The command text is hashed as written — no
/// normalization — so any edit to the SQL requires re-running <c>prepare</c>, which is exactly
/// the staleness signal the analyzer relies on.
/// </summary>
internal static class SnapshotKey
{
    public static string Compute(string commandText)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(commandText));
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var value in hash)
        {
            builder.Append(value.ToString("x2"));
        }

        return builder.ToString();
    }
}
