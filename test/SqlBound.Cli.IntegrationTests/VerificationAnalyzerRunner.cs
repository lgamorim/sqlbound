using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using SqlBound.Generators;

namespace SqlBound.Cli.IntegrationTests;

/// <summary>
/// Runs the public <see cref="SqlQueryVerificationAnalyzer"/> over a source text with the
/// snapshot files a real prepare run wrote to disk — the consumer's IDE experience in miniature.
/// </summary>
internal static class VerificationAnalyzerRunner
{
    private static readonly IReadOnlyList<MetadataReference> References = CreateReferences();

    public static async Task<IReadOnlyList<Diagnostic>> RunAsync(string source, string snapshotDirectory)
    {
        var compilation = CSharpCompilation.Create(
            "SqlBound.Cli.IntegrationTests.AnalyzerTarget",
            [CSharpSyntaxTree.ParseText(source)],
            References,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var additionalFiles = Directory.GetFiles(snapshotDirectory, "query-*.json")
            .Select(file => (AdditionalText)new FileAdditionalText(file))
            .ToImmutableArray();
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new SqlQueryVerificationAnalyzer()),
            new AnalyzerOptions(additionalFiles));
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

    private sealed class FileAdditionalText(string path) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) =>
            SourceText.From(File.ReadAllText(Path));
    }
}
