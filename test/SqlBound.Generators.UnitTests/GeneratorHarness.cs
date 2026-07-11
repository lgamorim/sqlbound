using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SqlBound.Generators.UnitTests;

/// <summary>
/// Runs <see cref="SqlQueryGenerator"/> against an in-memory compilation via the Roslyn
/// <see cref="CSharpGeneratorDriver"/>, the snapshot-testing entry point for all generator tests.
/// </summary>
internal static class GeneratorHarness
{
    private static readonly IReadOnlyList<MetadataReference> References = CreateReferences();

    public static GeneratorRunOutcome Run(string source)
    {
        var compilation = CSharpCompilation.Create(
            "SqlBound.Generators.UnitTests.Target",
            [CSharpSyntaxTree.ParseText(source)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver
            .Create(new SqlQueryGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out _);

        var result = driver.GetRunResult().Results.Single();
        return new GeneratorRunOutcome(
            result.GeneratedSources,
            result.Diagnostics,
            updatedCompilation.GetDiagnostics());
    }

    private static IReadOnlyList<MetadataReference> CreateReferences()
    {
        var trustedAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);
        var references = trustedAssemblies
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(SqlQueryAttribute).Assembly.Location));
        return references;
    }
}

/// <summary>The observable output of one generator run: sources, generator diagnostics, and the
/// diagnostics of the compilation after the generated sources were added to it.</summary>
internal sealed record GeneratorRunOutcome(
    IReadOnlyList<GeneratedSourceResult> GeneratedSources,
    IReadOnlyList<Diagnostic> GeneratorDiagnostics,
    IReadOnlyList<Diagnostic> CompilationDiagnostics);
