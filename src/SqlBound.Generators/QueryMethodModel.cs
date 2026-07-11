namespace SqlBound.Generators;

/// <summary>
/// The value-equatable model of one valid <c>[SqlQuery]</c> method, extracted from the semantic
/// model in the pipeline's transform step. Emission works exclusively from this model so that
/// incremental caching can skip regeneration whenever the model is unchanged.
/// </summary>
internal sealed record QueryMethodModel(
    string Namespace,
    EquatableArray<ContainingTypeModel> ContainingTypes,
    string Accessibility,
    string MethodName,
    bool IsExtensionMethod,
    string CommandText,
    string RowTypeText,
    EquatableArray<ColumnModel> Columns,
    EquatableArray<MethodParameterModel> Parameters);

/// <summary>One type in the (outermost-first) declaration chain wrapping the query method.</summary>
internal sealed record ContainingTypeModel(string Keyword, string Name);

/// <summary>One row-type constructor parameter, read from the result set column of the same name.</summary>
internal sealed record ColumnModel(string Name, string TypeText, string GetterInvocation, bool IsNullable);

/// <summary>One parameter of the query method, classified by the role it plays in the generated body.
/// <paramref name="CanBeNull"/> is meaningful for scalars only: it decides whether binding
/// coalesces the argument to <c>DBNull.Value</c>.</summary>
internal sealed record MethodParameterModel(string Name, string TypeText, ParameterKind Kind, bool CanBeNull = false);

/// <summary>The role a method parameter plays in the generated implementation.</summary>
internal enum ParameterKind
{
    Connection,
    Transaction,
    CancellationToken,
    Scalar,
}
