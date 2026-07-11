using System.Text;
using SqlBound.SqlServer;

namespace SqlBound.Cli;

/// <summary>
/// Serializes a described query into the <c>.sqlbound/</c> snapshot JSON the analyzer reads.
/// Hand-written for byte determinism across machines and operating systems (fixed field order,
/// two-space indent, <c>\n</c> line endings, trailing newline): snapshots are committed files,
/// so identical inputs must produce identical bytes for clean diffs and for
/// <c>prepare --check</c> to compare content directly.
/// </summary>
internal static class SnapshotWriter
{
    public const string Provider = "sqlserver";

    public static string FileName(string commandText) => $"query-{SnapshotKey.Compute(commandText)}.json";

    public static string Serialize(string commandText, QueryDescription description)
    {
        var builder = new StringBuilder();
        builder.Append("{\n");
        builder.Append($"  \"commandText\": {Quote(commandText)},\n");
        builder.Append($"  \"provider\": {Quote(Provider)},\n");

        if (description.Columns.Count == 0)
        {
            builder.Append("  \"columns\": [],\n");
        }
        else
        {
            builder.Append("  \"columns\": [\n");
            for (var i = 0; i < description.Columns.Count; i++)
            {
                var column = description.Columns[i];
                builder.Append(
                    $"    {{ \"ordinal\": {column.Ordinal}, \"name\": {Quote(column.Name)}, " +
                    $"\"sqlTypeName\": {Quote(column.SqlTypeName)}, \"clrTypeText\": {Quote(column.ClrTypeText)}, " +
                    $"\"isNullable\": {(column.IsNullable ? "true" : "false")} }}");
                builder.Append(i < description.Columns.Count - 1 ? ",\n" : "\n");
            }

            builder.Append("  ],\n");
        }

        if (description.Parameters.Count == 0)
        {
            builder.Append("  \"parameters\": []\n");
        }
        else
        {
            builder.Append("  \"parameters\": [\n");
            for (var i = 0; i < description.Parameters.Count; i++)
            {
                var parameter = description.Parameters[i];
                builder.Append(
                    $"    {{ \"name\": {Quote(parameter.Name)}, \"sqlTypeName\": {Quote(parameter.SqlTypeName)}, " +
                    $"\"clrTypeText\": {Quote(parameter.ClrTypeText)} }}");
                builder.Append(i < description.Parameters.Count - 1 ? ",\n" : "\n");
            }

            builder.Append("  ]\n");
        }

        builder.Append("}\n");
        return builder.ToString();
    }

    private static string Quote(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case < ' ':
                    builder.Append($"\\u{(int)character:x4}");
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        builder.Append('"');
        return builder.ToString();
    }
}
