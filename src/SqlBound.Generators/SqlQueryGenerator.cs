using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SqlBound.Generators;

/// <summary>
/// Incremental source generator that implements <c>[SqlQuery]</c>-annotated <c>static partial</c>
/// methods with straight-line, reflection-free <c>DbDataReader</c> materialization code. Per
/// ADR 0002 the generator is signature-driven: its only inputs are the attribute and the declared
/// method signature — it never reads <c>.sqlbound/</c> snapshots or performs I/O.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SqlQueryGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var results = context.SyntaxProvider.ForAttributeWithMetadataName(
            "SqlBound.SqlQueryAttribute",
            static (node, _) => node is MethodDeclarationSyntax,
            static (attributeContext, _) => QueryMethodParser.Parse(attributeContext));

        context.RegisterSourceOutput(results, static (outputContext, result) =>
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                outputContext.ReportDiagnostic(diagnostic.CreateDiagnostic());
            }

            // result.Method emission arrives in the next M4 commit.
        });
    }
}
