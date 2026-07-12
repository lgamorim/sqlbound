namespace SqlBound.Introspection;

/// <summary>
/// The metadata a provider reports for a command text: its first result set's columns and its
/// parameters. This is what the <c>prepare</c> step snapshots and the analyzer later compares
/// against a <c>[SqlQuery]</c>/<c>[SqlExecute]</c> method's declared signature.
/// </summary>
/// <param name="Columns">The visible columns of the first result set, in ordinal order; empty when the statement produces no result set.</param>
/// <param name="Parameters">The parameters the statement uses, in ordinal order.</param>
public sealed record QueryDescription(
    IReadOnlyList<DescribedColumn> Columns,
    IReadOnlyList<DescribedParameter> Parameters);

/// <summary>A single result-set column as described by the provider.</summary>
/// <param name="Ordinal">Zero-based position of the column in the result set, matching <see cref="System.Data.Common.DbDataReader"/> ordinals.</param>
/// <param name="Name">The column name; empty for an unnamed expression column.</param>
/// <param name="SqlTypeName">The provider's type name as reported, including any precision/scale/length suffix (e.g. <c>decimal(18,2)</c>).</param>
/// <param name="ClrTypeText">The C# type text of the generator-supported CLR type the column materializes to, without a nullability marker.</param>
/// <param name="IsNullable">Whether the provider reports the column as nullable.</param>
public sealed record DescribedColumn(
    int Ordinal,
    string Name,
    string SqlTypeName,
    string ClrTypeText,
    bool IsNullable);

/// <summary>A single command parameter as described by the provider.</summary>
/// <param name="Name">The parameter name without its provider-specific marker (e.g. leading <c>@</c>), matching the C# method parameter it binds to.</param>
/// <param name="SqlTypeName">The provider's type name suggested for the parameter, including any precision/scale/length suffix; empty when the provider has no static parameter typing.</param>
/// <param name="ClrTypeText">The C# type text of the generator-supported CLR type the parameter binds from, without a nullability marker; <see langword="null"/> when the provider cannot infer one (e.g. SQLite has no static parameter typing), in which case the analyzer skips the type-mismatch check for this parameter.</param>
public sealed record DescribedParameter(
    string Name,
    string SqlTypeName,
    string? ClrTypeText);
