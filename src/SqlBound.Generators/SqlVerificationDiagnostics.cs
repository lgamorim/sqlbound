using Microsoft.CodeAnalysis;

namespace SqlBound.Generators;

/// <summary>
/// Descriptors for the snapshot-based verification diagnostics (SQLB1xx): mismatches between a
/// query method's declared signature and the database-described metadata committed under
/// <c>.sqlbound/</c>. Distinct from <see cref="SqlQueryDiagnostics"/> (SQLB0xx), which validates
/// how the attributes are used and never needs a snapshot.
/// </summary>
internal static class SqlVerificationDiagnostics
{
    private const string Category = "SqlBound.Verification";

    public static readonly DiagnosticDescriptor QueryHasNoSnapshot = new(
        "SQLB101",
        "Query has no snapshot",
        "No .sqlbound snapshot exists for the query in '{0}'; run the prepare step to describe it against the database",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SnapshotInvalidOrStale = new(
        "SQLB102",
        "Snapshot is invalid or stale",
        "Snapshot '{0}' is unreadable or no longer matches the query's command text; re-run the prepare step",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor StatementProducesNoResultSet = new(
        "SQLB103",
        "Statement produces no result set",
        "'{0}' expects a result set, but the database reports the statement produces none",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ResultSetColumnMissing = new(
        "SQLB104",
        "Result set column missing",
        "The result set has no column named '{0}', which '{1}' reads",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ColumnTypeMismatch = new(
        "SQLB105",
        "Column type mismatch",
        "Column '{0}' is '{1}' ({2}) in the database, but '{3}' declares '{4}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ColumnNullabilityMismatch = new(
        "SQLB106",
        "Column nullability mismatch",
        "Column '{0}' is nullable in the database, but '{1}' declares it non-nullable",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ResultSetHasUnreadColumns = new(
        "SQLB107",
        "Result set has unread columns",
        "The result set returns columns '{0}' never reads: {1}",
        Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SqlParameterMissingFromMethod = new(
        "SQLB108",
        "SQL parameter missing from the method",
        "The statement uses parameter '@{0}', but '{1}' declares no matching parameter",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MethodParameterUnusedBySql = new(
        "SQLB109",
        "Method parameter unused by the SQL",
        "Parameter '{0}' of '{1}' is never used by the statement",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ParameterTypeMismatch = new(
        "SQLB110",
        "Parameter type mismatch",
        "Parameter '{0}' is '{1}' ({2}) in the database, but '{3}' declares '{4}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ExecuteStatementReturnsResultSet = new(
        "SQLB111",
        "Execute statement returns a result set",
        "The statement returns a result set that '{0}' discards; use [SqlQuery] to read it",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
