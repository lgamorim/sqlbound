using System.Diagnostics.CodeAnalysis;

namespace SqlBound.Sqlite;

/// <summary>
/// Maps SQLite declared column types (as reported by <c>sqlite3_column_decltype</c>, e.g.
/// <c>NVARCHAR(50)</c>) to the C# type text the SqlBound generator uses for the corresponding
/// <see cref="System.Data.Common.DbDataReader"/> getter. SQLite itself only has five storage
/// classes (INTEGER, REAL, TEXT, BLOB, NULL); the declared type is a naming convention the schema
/// author chose to signal intent, not an enforced storage format, so this mirrors the SQL Server
/// mapping's declared-type vocabulary rather than SQLite's own coarser 5-affinity-class rules.
/// </summary>
internal static class SqliteTypeMap
{
    public static bool TryMap(string declaredType, [NotNullWhen(true)] out string? clrTypeText)
    {
        var parenthesisIndex = declaredType.IndexOf('(');
        var baseName = parenthesisIndex >= 0 ? declaredType[..parenthesisIndex] : declaredType;
        clrTypeText = baseName.Trim().ToUpperInvariant() switch
        {
            "BOOLEAN" or "BOOL" => "bool",
            "TINYINT" => "byte",
            "SMALLINT" or "INT2" => "short",
            "INT" or "INTEGER" or "MEDIUMINT" => "int",
            "BIGINT" or "INT8" or "UNSIGNED BIG INT" => "long",
            "REAL" or "DOUBLE" or "DOUBLE PRECISION" or "FLOAT" => "double",
            "DECIMAL" or "NUMERIC" or "MONEY" => "decimal",
            "CHARACTER" or "VARCHAR" or "VARYING CHARACTER" or "NCHAR" or "NVARCHAR" or "TEXT" or "CLOB" => "string",
            "BLOB" or "BINARY" or "VARBINARY" => "byte[]",
            "GUID" or "UNIQUEIDENTIFIER" => "global::System.Guid",
            "DATE" or "DATETIME" or "TIMESTAMP" => "global::System.DateTime",
            _ => null,
        };
        return clrTypeText is not null;
    }
}
