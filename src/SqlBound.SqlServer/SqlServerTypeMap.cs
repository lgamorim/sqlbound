using System.Diagnostics.CodeAnalysis;

namespace SqlBound.SqlServer;

/// <summary>
/// Maps SQL Server system type names (as reported by <c>sp_describe_first_result_set</c> and
/// <c>sp_describe_undeclared_parameters</c>, e.g. <c>nvarchar(50)</c>) to the C# type text the
/// SqlBound generator uses for the corresponding <see cref="System.Data.Common.DbDataReader"/>
/// getter. The supported set is deliberately exactly the generator's: a SQL type outside it has
/// no reflection-free materialization path and must surface as a describe error, not a mapping.
/// </summary>
internal static class SqlServerTypeMap
{
    public static bool TryMap(string sqlTypeName, [NotNullWhen(true)] out string? clrTypeText)
    {
        var parenthesisIndex = sqlTypeName.IndexOf('(');
        var baseName = parenthesisIndex >= 0 ? sqlTypeName[..parenthesisIndex] : sqlTypeName;
        clrTypeText = baseName.Trim().ToLowerInvariant() switch
        {
            "bit" => "bool",
            "tinyint" => "byte",
            "smallint" => "short",
            "int" => "int",
            "bigint" => "long",
            "real" => "float",
            "float" => "double",
            "decimal" or "numeric" or "money" or "smallmoney" => "decimal",
            "char" or "varchar" or "nchar" or "nvarchar" or "text" or "ntext" => "string",
            "binary" or "varbinary" or "image" or "rowversion" or "timestamp" => "byte[]",
            "uniqueidentifier" => "global::System.Guid",
            "date" or "smalldatetime" or "datetime" or "datetime2" => "global::System.DateTime",
            _ => null,
        };
        return clrTypeText is not null;
    }
}
