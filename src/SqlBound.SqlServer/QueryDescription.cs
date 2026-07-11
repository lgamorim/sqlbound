namespace SqlBound.SqlServer;

/// <summary>
/// The metadata SQL Server reports for a command text: its first result set's columns and its
/// undeclared parameters. This is what the <c>prepare</c> step snapshots and the analyzer later
/// compares against a <c>[SqlQuery]</c>/<c>[SqlExecute]</c> method's declared signature.
/// </summary>
/// <param name="Columns">The visible columns of the first result set, in ordinal order; empty when the statement produces no result set.</param>
/// <param name="Parameters">The parameters the statement uses, in ordinal order.</param>
public sealed record QueryDescription(
    IReadOnlyList<DescribedColumn> Columns,
    IReadOnlyList<DescribedParameter> Parameters);

/// <summary>A single result-set column as described by SQL Server.</summary>
/// <param name="Ordinal">Zero-based position of the column in the result set, matching <see cref="System.Data.Common.DbDataReader"/> ordinals.</param>
/// <param name="Name">The column name; empty for an unnamed expression column.</param>
/// <param name="SqlTypeName">The SQL Server system type name as reported, including any precision/scale/length suffix (e.g. <c>decimal(18,2)</c>).</param>
/// <param name="ClrTypeText">The C# type text of the generator-supported CLR type the column materializes to, without a nullability marker.</param>
/// <param name="IsNullable">Whether SQL Server reports the column as nullable.</param>
public sealed record DescribedColumn(
    int Ordinal,
    string Name,
    string SqlTypeName,
    string ClrTypeText,
    bool IsNullable);

/// <summary>A single command parameter as described by SQL Server.</summary>
/// <param name="Name">The parameter name without the leading <c>@</c>, matching the C# method parameter it binds to.</param>
/// <param name="SqlTypeName">The SQL Server system type name suggested for the parameter, including any precision/scale/length suffix.</param>
/// <param name="ClrTypeText">The C# type text of the generator-supported CLR type the parameter binds from, without a nullability marker.</param>
public sealed record DescribedParameter(
    string Name,
    string SqlTypeName,
    string ClrTypeText);
