using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SqlBound.Cli;

/// <summary>
/// Finds <c>[SqlQuery]</c>/<c>[SqlExecute]</c> command texts by walking C# syntax — no project
/// compilation, so discovery is fast and needs no build. The trade-off (documented): command
/// texts must be inline string literals (regular, verbatim, or raw, optionally concatenated);
/// SQL referenced through a <c>const</c> is reported as a warning instead of silently missed.
/// </summary>
internal static class QueryDiscovery
{
    public static DiscoveryResult DiscoverFromDirectory(string directory)
    {
        var queries = new List<DiscoveredQuery>();
        var warnings = new List<string>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(directory, file);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(segment => segment is "bin" or "obj"))
            {
                continue;
            }

            var result = DiscoverFromSource(File.ReadAllText(file), relative);
            queries.AddRange(result.Queries);
            warnings.AddRange(result.Warnings);
        }

        return new DiscoveryResult(queries, warnings);
    }

    public static DiscoveryResult DiscoverFromSource(string source, string filePath)
    {
        var queries = new List<DiscoveredQuery>();
        var warnings = new List<string>();
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        foreach (var attribute in root.DescendantNodes().OfType<AttributeSyntax>())
        {
            var isExecute = false;
            switch (UnqualifiedName(attribute))
            {
                case "SqlQuery" or "SqlQueryAttribute":
                    break;
                case "SqlExecute" or "SqlExecuteAttribute":
                    isExecute = true;
                    break;
                default:
                    continue;
            }

            if (attribute.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } method)
            {
                continue;
            }

            var argument = attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
            if (argument is not null && TryGetLiteralText(argument, out var commandText))
            {
                queries.Add(new DiscoveredQuery(commandText, method.Identifier.ValueText, filePath, isExecute));
            }
            else
            {
                warnings.Add(
                    $"{filePath}: the command text of '{method.Identifier.ValueText}' is not a string literal; " +
                    "prepare cannot describe it.");
            }
        }

        return new DiscoveryResult(queries, warnings);
    }

    private static string UnqualifiedName(AttributeSyntax attribute) => attribute.Name switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => string.Empty,
    };

    private static bool TryGetLiteralText(ExpressionSyntax expression, out string text)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                text = literal.Token.ValueText;
                return true;
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression):
                if (TryGetLiteralText(binary.Left, out var left) && TryGetLiteralText(binary.Right, out var right))
                {
                    text = left + right;
                    return true;
                }

                text = string.Empty;
                return false;
            case ParenthesizedExpressionSyntax parenthesized:
                return TryGetLiteralText(parenthesized.Expression, out text);
            default:
                text = string.Empty;
                return false;
        }
    }
}

/// <summary>One discovered query: its command text and where it was declared.</summary>
internal sealed record DiscoveredQuery(string CommandText, string MethodName, string FilePath, bool IsExecute);

/// <summary>Everything one discovery pass found: queries plus warnings for SQL it had to skip.</summary>
internal sealed record DiscoveryResult(IReadOnlyList<DiscoveredQuery> Queries, IReadOnlyList<string> Warnings);
