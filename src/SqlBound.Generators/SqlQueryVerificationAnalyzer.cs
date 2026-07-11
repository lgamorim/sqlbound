using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SqlBound.Generators;

/// <summary>
/// Verifies <c>[SqlQuery]</c>/<c>[SqlExecute]</c> methods against the committed <c>.sqlbound/</c>
/// snapshots wired in as <c>AdditionalFiles</c>, per ADR 0001: this analyzer never opens a
/// database connection — the snapshots are its only source of database truth. Verification is
/// opt-in by presence (ADR 0003): with no <c>.sqlbound/</c> files at all the analyzer stays
/// silent, so codegen-only consumers are never nagged; once any snapshot is wired, a query
/// without one is reported as stale coverage (SQLB101).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SqlQueryVerificationAnalyzer : DiagnosticAnalyzer
{
    private const string QueryAttributeName = "SqlBound.SqlQueryAttribute";
    private const string ExecuteAttributeName = "SqlBound.SqlExecuteAttribute";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            SqlVerificationDiagnostics.QueryHasNoSnapshot,
            SqlVerificationDiagnostics.SnapshotInvalidOrStale,
            SqlVerificationDiagnostics.StatementProducesNoResultSet,
            SqlVerificationDiagnostics.ResultSetColumnMissing,
            SqlVerificationDiagnostics.ColumnTypeMismatch,
            SqlVerificationDiagnostics.ColumnNullabilityMismatch,
            SqlVerificationDiagnostics.ResultSetHasUnreadColumns,
            SqlVerificationDiagnostics.SqlParameterMissingFromMethod,
            SqlVerificationDiagnostics.MethodParameterUnusedBySql,
            SqlVerificationDiagnostics.ParameterTypeMismatch,
            SqlVerificationDiagnostics.ExecuteStatementReturnsResultSet);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var snapshots = LoadSnapshots(context.Options.AdditionalFiles, context.CancellationToken);
        if (snapshots is null)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            nodeContext => VerifyMethod(nodeContext, snapshots), SyntaxKind.MethodDeclaration);
    }

    /// <summary>
    /// Loads every snapshot under a <c>.sqlbound/</c> directory, keyed by the hash embedded in
    /// the file name. Returns <c>null</c> when no <c>.sqlbound/</c> files are wired at all —
    /// the not-opted-in signal.
    /// </summary>
    private static Dictionary<string, SnapshotEntry>? LoadSnapshots(
        ImmutableArray<AdditionalText> additionalFiles, CancellationToken cancellationToken)
    {
        Dictionary<string, SnapshotEntry>? snapshots = null;
        foreach (var file in additionalFiles)
        {
            var normalizedPath = file.Path.Replace('\\', '/');
            if (!normalizedPath.Contains("/.sqlbound/") && !normalizedPath.StartsWith(".sqlbound/", StringComparison.Ordinal))
            {
                continue;
            }

            // Any .sqlbound/ file opts the project in, even one this analyzer version cannot key.
            snapshots ??= new Dictionary<string, SnapshotEntry>(StringComparer.Ordinal);
            var fileName = normalizedPath.Substring(normalizedPath.LastIndexOf('/') + 1);
            if (!TryReadKeyFromFileName(fileName, out var key))
            {
                continue;
            }

            var text = file.GetText(cancellationToken)?.ToString();
            QuerySnapshot? snapshot = null;
            if (text is not null && QuerySnapshotReader.TryRead(text, out var parsed))
            {
                snapshot = parsed;
            }

            snapshots[key] = new SnapshotEntry(fileName, snapshot);
        }

        return snapshots;
    }

    private static bool TryReadKeyFromFileName(string fileName, out string key)
    {
        const string prefix = "query-";
        const string suffix = ".json";
        key = string.Empty;
        if (!fileName.StartsWith(prefix, StringComparison.Ordinal)
            || !fileName.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var hex = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - suffix.Length);
        if (hex.Length != 64)
        {
            return false;
        }

        foreach (var character in hex)
        {
            if (character is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
            {
                return false;
            }
        }

        key = hex;
        return true;
    }

    private static void VerifyMethod(SyntaxNodeAnalysisContext context, Dictionary<string, SnapshotEntry> snapshots)
    {
        var node = (MethodDeclarationSyntax)context.Node;
        if (node.AttributeLists.Count == 0
            || context.SemanticModel.GetDeclaredSymbol(node, context.CancellationToken) is not { } symbol)
        {
            return;
        }

        AttributeData? attribute = null;
        var isExecute = false;
        foreach (var candidate in symbol.GetAttributes())
        {
            var attributeName = candidate.AttributeClass?.ToDisplayString();
            if (attributeName is not (QueryAttributeName or ExecuteAttributeName))
            {
                continue;
            }

            // Partial methods surface one symbol from two declarations; act only on the node
            // that syntactically carries the attribute so each method verifies exactly once.
            if (candidate.ApplicationSyntaxReference is { } reference
                && reference.SyntaxTree == node.SyntaxTree
                && node.FullSpan.Contains(reference.Span))
            {
                attribute = candidate;
                isExecute = attributeName == ExecuteAttributeName;
                break;
            }
        }

        if (attribute is null)
        {
            return;
        }

        var result = QueryMethodParser.Parse(symbol, node, attribute, context.SemanticModel.Compilation, isExecute);
        if (result.Method is not { } model)
        {
            // Usage errors (SQLB0xx) are the generator's to report; nothing to verify.
            return;
        }

        var location = node.Identifier.GetLocation();
        if (!snapshots.TryGetValue(SnapshotKey.Compute(model.CommandText), out var entry))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                SqlVerificationDiagnostics.QueryHasNoSnapshot, location, model.MethodName));
            return;
        }

        if (entry.Snapshot is null || entry.Snapshot.CommandText != model.CommandText)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                SqlVerificationDiagnostics.SnapshotInvalidOrStale, location, entry.FileName));
            return;
        }

        foreach (var finding in QueryVerifier.Verify(model, entry.Snapshot))
        {
            object[] messageArgs = [.. finding.MessageArgs];
            context.ReportDiagnostic(Diagnostic.Create(finding.Descriptor, location, messageArgs));
        }
    }

    private sealed record SnapshotEntry(string FileName, QuerySnapshot? Snapshot);
}
