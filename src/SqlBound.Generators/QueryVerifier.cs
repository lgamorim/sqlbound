using Microsoft.CodeAnalysis;

namespace SqlBound.Generators;

/// <summary>
/// Compares a parsed query method against its committed snapshot and yields the verification
/// findings. Pure and location-free: the analyzer attaches locations when reporting. Column
/// matching is by name (case-insensitive), not ordinal, because the generated code binds columns
/// with <c>GetOrdinal(name)</c>; types compare by mapped CLR type text, not SQL type names,
/// because the database's suggested SQL types are inferences (e.g. a widened decimal).
/// </summary>
internal static class QueryVerifier
{
    public static IReadOnlyList<VerificationFinding> Verify(QueryMethodModel model, QuerySnapshot snapshot)
    {
        var findings = new List<VerificationFinding>();
        VerifyColumns(model, snapshot, findings);
        VerifyParameters(model, snapshot, findings);
        return findings;
    }

    private static void VerifyColumns(QueryMethodModel model, QuerySnapshot snapshot, List<VerificationFinding> findings)
    {
        if (model.Shape is ResultShape.Execute or ResultShape.ExecuteDiscard)
        {
            if (snapshot.Columns.Count > 0)
            {
                findings.Add(new VerificationFinding(
                    SqlVerificationDiagnostics.ExecuteStatementReturnsResultSet, model.MethodName));
            }

            return;
        }

        if (snapshot.Columns.Count == 0)
        {
            findings.Add(new VerificationFinding(
                SqlVerificationDiagnostics.StatementProducesNoResultSet, model.MethodName));
            return;
        }

        if (model.ElementKind == ResultElementKind.Scalar)
        {
            var first = snapshot.Columns[0];
            var declared = model.Columns[0];
            CompareColumn(model, declared, first, DisplayName(first), findings);
            if (snapshot.Columns.Count > 1)
            {
                ReportUnreadColumns(model, snapshot.Columns.Skip(1), findings);
            }

            return;
        }

        var byName = new Dictionary<string, SnapshotColumn>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in snapshot.Columns)
        {
            // On duplicate names keep the first: that is the one GetOrdinal resolves.
            if (!byName.ContainsKey(column.Name))
            {
                byName.Add(column.Name, column);
            }
        }

        var readNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var declared in model.Columns)
        {
            if (!byName.TryGetValue(declared.Name, out var described))
            {
                findings.Add(new VerificationFinding(
                    SqlVerificationDiagnostics.ResultSetColumnMissing, declared.Name, model.MethodName));
                continue;
            }

            readNames.Add(described.Name);
            CompareColumn(model, declared, described, described.Name, findings);
        }

        var unread = snapshot.Columns.Where(column => !readNames.Contains(column.Name)).ToArray();
        if (unread.Length > 0)
        {
            ReportUnreadColumns(model, unread, findings);
        }
    }

    private static void VerifyParameters(QueryMethodModel model, QuerySnapshot snapshot, List<VerificationFinding> findings)
    {
        var declaredByName = new Dictionary<string, MethodParameterModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in model.Parameters)
        {
            if (parameter.Kind == ParameterKind.Scalar)
            {
                declaredByName[parameter.Name] = parameter;
            }
        }

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var described in snapshot.Parameters)
        {
            if (!declaredByName.TryGetValue(described.Name, out var declared))
            {
                findings.Add(new VerificationFinding(
                    SqlVerificationDiagnostics.SqlParameterMissingFromMethod, described.Name, model.MethodName));
                continue;
            }

            usedNames.Add(declared.Name);
            // A null ClrTypeText means the provider has no static parameter typing (e.g. SQLite) -
            // there is nothing to compare the declared C# type against.
            if (described.ClrTypeText is not null && StripNullableSuffix(declared.TypeText) != described.ClrTypeText)
            {
                findings.Add(new VerificationFinding(
                    SqlVerificationDiagnostics.ParameterTypeMismatch,
                    declared.Name,
                    described.ClrTypeText,
                    described.SqlTypeName,
                    model.MethodName,
                    declared.TypeText));
            }
        }

        foreach (var parameter in model.Parameters)
        {
            if (parameter.Kind == ParameterKind.Scalar && !usedNames.Contains(parameter.Name))
            {
                findings.Add(new VerificationFinding(
                    SqlVerificationDiagnostics.MethodParameterUnusedBySql, parameter.Name, model.MethodName));
            }
        }
    }

    private static void CompareColumn(
        QueryMethodModel model,
        ColumnModel declared,
        SnapshotColumn described,
        string columnDisplayName,
        List<VerificationFinding> findings)
    {
        if (StripNullableSuffix(declared.TypeText) != described.ClrTypeText)
        {
            findings.Add(new VerificationFinding(
                SqlVerificationDiagnostics.ColumnTypeMismatch,
                columnDisplayName,
                described.ClrTypeText,
                described.SqlTypeName,
                model.MethodName,
                declared.TypeText));
            return;
        }

        if (described.IsNullable && !declared.IsNullable)
        {
            findings.Add(new VerificationFinding(
                SqlVerificationDiagnostics.ColumnNullabilityMismatch, columnDisplayName, model.MethodName));
        }
    }

    private static void ReportUnreadColumns(
        QueryMethodModel model, IEnumerable<SnapshotColumn> unread, List<VerificationFinding> findings)
    {
        findings.Add(new VerificationFinding(
            SqlVerificationDiagnostics.ResultSetHasUnreadColumns,
            model.MethodName,
            string.Join(", ", unread.Select(DisplayName))));
    }

    private static string DisplayName(SnapshotColumn column) =>
        column.Name.Length > 0 ? column.Name : $"#{column.Ordinal}";

    private static string StripNullableSuffix(string typeText) =>
        typeText.Length > 0 && typeText[typeText.Length - 1] == '?'
            ? typeText.Substring(0, typeText.Length - 1)
            : typeText;
}

/// <summary>One verification finding: a descriptor plus its message arguments, no location.</summary>
internal sealed record VerificationFinding(DiagnosticDescriptor Descriptor, EquatableArray<string> MessageArgs)
{
    public VerificationFinding(DiagnosticDescriptor descriptor, params string[] args)
        : this(descriptor, new EquatableArray<string>(args))
    {
    }
}
