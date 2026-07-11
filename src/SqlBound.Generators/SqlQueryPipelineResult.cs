namespace SqlBound.Generators;

/// <summary>
/// The outcome of parsing one <c>[SqlQuery]</c> method: either a model to emit from, or the usage
/// diagnostics explaining why no implementation can be generated.
/// </summary>
internal sealed record SqlQueryPipelineResult(
    QueryMethodModel? Method,
    EquatableArray<DiagnosticInfo> Diagnostics);
