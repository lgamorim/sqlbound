using System.Diagnostics.CodeAnalysis;

namespace SqlBound.Npgsql;

/// <summary>
/// Maps PostgreSQL type names (as reported by <c>NpgsqlDbColumn.DataTypeName</c> /
/// <c>NpgsqlParameter.DataTypeName</c>, e.g. <c>character varying</c>) to the C# type text the
/// SqlBound generator uses for the corresponding <see cref="System.Data.Common.DbDataReader"/>
/// getter. Unlike SQL Server and SQLite, Postgres's reported type names never carry a
/// precision/scale/length suffix, so no stripping is needed before the lookup.
/// </summary>
internal static class PostgresTypeMap
{
    public static bool TryMap(string dataTypeName, [NotNullWhen(true)] out string? clrTypeText)
    {
        clrTypeText = dataTypeName.Trim().ToLowerInvariant() switch
        {
            "boolean" => "bool",
            "smallint" => "short",
            "integer" => "int",
            "bigint" => "long",
            "real" => "float",
            "double precision" => "double",
            "numeric" or "money" => "decimal",
            "character" or "character varying" or "text" => "string",
            "bytea" => "byte[]",
            "uuid" => "global::System.Guid",
            "date" or "timestamp without time zone" => "global::System.DateTime",
            _ => null,
        };
        return clrTypeText is not null;
    }
}
