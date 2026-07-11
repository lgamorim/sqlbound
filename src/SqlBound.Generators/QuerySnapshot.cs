namespace SqlBound.Generators;

/// <summary>
/// The analyzer-side view of one committed <c>.sqlbound/</c> snapshot: the database-described
/// metadata for a single command text, as written by the <c>prepare</c> step. Mirrors the
/// provider describe result (M7's <c>QueryDescription</c>) but lives here because the analyzer
/// targets netstandard2.0 and reads only JSON, never provider assemblies.
/// </summary>
internal sealed record QuerySnapshot(
    string CommandText,
    string Provider,
    EquatableArray<SnapshotColumn> Columns,
    EquatableArray<SnapshotParameter> Parameters);

internal sealed record SnapshotColumn(
    int Ordinal,
    string Name,
    string SqlTypeName,
    string ClrTypeText,
    bool IsNullable);

internal sealed record SnapshotParameter(
    string Name,
    string SqlTypeName,
    string ClrTypeText);
