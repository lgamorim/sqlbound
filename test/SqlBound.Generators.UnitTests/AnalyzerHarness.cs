using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace SqlBound.Generators.UnitTests;

/// <summary>
/// Runs <see cref="SqlQueryVerificationAnalyzer"/> against an in-memory compilation with in-memory
/// additional files standing in for the committed <c>.sqlbound/</c> snapshots.
/// </summary>
internal static class AnalyzerHarness
{
    private static readonly IReadOnlyList<MetadataReference> References = CreateReferences();

    public static async Task<IReadOnlyList<Diagnostic>> RunAsync(
        string source, params (string Path, string Content)[] additionalFiles)
    {
        var compilation = CSharpCompilation.Create(
            "SqlBound.Generators.UnitTests.AnalyzerTarget",
            [CSharpSyntaxTree.ParseText(source)],
            References,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var options = new AnalyzerOptions(
            [.. additionalFiles.Select(file => (AdditionalText)new InMemoryAdditionalText(file.Path, file.Content))]);
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new SqlQueryVerificationAnalyzer()), options);
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
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

    private sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) =>
            SourceText.From(content);
    }
}
