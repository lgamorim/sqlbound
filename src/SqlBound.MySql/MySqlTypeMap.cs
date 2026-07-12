using System.Diagnostics.CodeAnalysis;

namespace SqlBound.MySql;

/// <summary>
/// Maps MySQL type names (as reported by <c>MySqlDataReader.GetColumnSchemaAsync</c>'s
/// <c>DataTypeName</c>, e.g. <c>CHAR(10)</c>) to the C# type text the SqlBound generator uses for
/// the corresponding <see cref="System.Data.Common.DbDataReader"/> getter. MySqlConnector already
/// distinguishes a <c>BOOLEAN</c>-declared column (reported as <c>BOOL</c>) from a genuine
/// <c>TINYINT</c>, so no length-based heuristic is needed to tell them apart.
/// </summary>
internal static class MySqlTypeMap
{
    public static bool TryMap(string dataTypeName, [NotNullWhen(true)] out string? clrTypeText)
    {
        var parenthesisIndex = dataTypeName.IndexOf('(');
        var baseName = parenthesisIndex >= 0 ? dataTypeName[..parenthesisIndex] : dataTypeName;
        clrTypeText = baseName.Trim().ToUpperInvariant() switch
        {
            "BOOL" or "BOOLEAN" => "bool",
            "TINYINT" => "byte",
            "SMALLINT" => "short",
            "MEDIUMINT" or "INT" or "INTEGER" => "int",
            "BIGINT" => "long",
            "FLOAT" => "float",
            "DOUBLE" => "double",
            "DECIMAL" or "NUMERIC" => "decimal",
            "CHAR" or "VARCHAR" => "string",
            "BLOB" => "byte[]",
            "DATE" or "DATETIME" or "TIMESTAMP" => "global::System.DateTime",
            _ => null,
        };
        return clrTypeText is not null;
    }
}
