using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SqlBound.Generators;

/// <summary>
/// Turns one <c>[SqlQuery]</c> attribute occurrence into a <see cref="SqlQueryPipelineResult"/>:
/// a <see cref="QueryMethodModel"/> when the method is well-formed, usage diagnostics otherwise.
/// </summary>
internal static class QueryMethodParser
{
    private static readonly SymbolDisplayFormat TypeTextFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static SqlQueryPipelineResult Parse(GeneratorAttributeSyntaxContext context)
    {
        var symbol = (IMethodSymbol)context.TargetSymbol;
        var syntax = (MethodDeclarationSyntax)context.TargetNode;
        var location = LocationInfo.From(syntax.Identifier.GetLocation());
        var compilation = context.SemanticModel.Compilation;
        var diagnostics = new List<DiagnosticInfo>();

        void Report(DiagnosticDescriptor descriptor, params string[] args) =>
            diagnostics.Add(new DiagnosticInfo(descriptor, location, new EquatableArray<string>(args)));

        var constructorArguments = context.Attributes[0].ConstructorArguments;
        var commandText = constructorArguments.Length == 1 ? constructorArguments[0].Value as string : null;
        if (string.IsNullOrWhiteSpace(commandText))
        {
            Report(SqlQueryDiagnostics.CommandTextMustNotBeEmpty);
        }

        var isPartialDefinition = syntax.Modifiers.Any(SyntaxKind.PartialKeyword)
            && syntax.Body is null
            && syntax.ExpressionBody is null;
        if (!isPartialDefinition || symbol.PartialImplementationPart is not null)
        {
            Report(SqlQueryDiagnostics.MethodMustBePartialDefinition, symbol.Name);
        }

        if (!symbol.IsStatic)
        {
            Report(SqlQueryDiagnostics.MethodMustBeStatic, symbol.Name);
        }

        if (symbol.Arity > 0 || ContainingTypeChain(symbol).Any(type => type.Arity > 0))
        {
            Report(SqlQueryDiagnostics.GenericDeclarationsNotSupported, symbol.Name);
        }

        var dbConnectionType = compilation.GetTypeByMetadataName("System.Data.Common.DbConnection");
        var dbTransactionType = compilation.GetTypeByMetadataName("System.Data.Common.DbTransaction");
        var cancellationTokenType = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
        var guidType = compilation.GetTypeByMetadataName("System.Guid");

        var parameters = new List<MethodParameterModel>();
        var methodParameters = symbol.Parameters;
        if (methodParameters.Length == 0 || !DerivesFrom(methodParameters[0].Type, dbConnectionType))
        {
            Report(SqlQueryDiagnostics.MethodMustTakeDbConnectionFirst, symbol.Name);
        }
        else
        {
            parameters.Add(new MethodParameterModel(
                methodParameters[0].Name, TypeText(methodParameters[0].Type), ParameterKind.Connection));
        }

        for (var i = 1; i < methodParameters.Length; i++)
        {
            var parameter = methodParameters[i];
            var typeText = TypeText(parameter.Type);
            if (parameter.RefKind != RefKind.None)
            {
                Report(SqlQueryDiagnostics.UnsupportedQueryParameterType, parameter.Name, typeText);
            }
            else if (i == 1 && DerivesFrom(StripNullable(parameter.Type), dbTransactionType))
            {
                parameters.Add(new MethodParameterModel(parameter.Name, typeText, ParameterKind.Transaction));
            }
            else if (i == methodParameters.Length - 1
                && SymbolEqualityComparer.Default.Equals(parameter.Type, cancellationTokenType))
            {
                parameters.Add(new MethodParameterModel(parameter.Name, typeText, ParameterKind.CancellationToken));
            }
            else if (TryGetGetter(parameter.Type, guidType, out _))
            {
                var canBeNull = parameter.Type.IsReferenceType || IsNullable(parameter.Type);
                parameters.Add(new MethodParameterModel(parameter.Name, typeText, ParameterKind.Scalar, canBeNull));
            }
            else
            {
                Report(SqlQueryDiagnostics.UnsupportedQueryParameterType, parameter.Name, typeText);
            }
        }

        var taskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
        var readOnlyListType = compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyList`1");
        ITypeSymbol? rowType = null;
        if (symbol.ReturnType is INamedTypeSymbol task
            && SymbolEqualityComparer.Default.Equals(task.OriginalDefinition, taskType)
            && task.TypeArguments[0] is INamedTypeSymbol list
            && SymbolEqualityComparer.Default.Equals(list.OriginalDefinition, readOnlyListType))
        {
            rowType = list.TypeArguments[0];
        }
        else
        {
            Report(SqlQueryDiagnostics.UnsupportedReturnType, symbol.Name, symbol.ReturnType.ToDisplayString());
        }

        ColumnModel[] columns = rowType is null ? [] : ParseColumns(rowType, guidType, Report);

        if (diagnostics.Count > 0)
        {
            return new SqlQueryPipelineResult(null, new EquatableArray<DiagnosticInfo>([.. diagnostics]));
        }

        var containingTypes = ContainingTypeChain(symbol)
            .Reverse()
            .Select(type => new ContainingTypeModel(TypeKeyword(type), type.Name))
            .ToArray();

        var model = new QueryMethodModel(
            symbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : symbol.ContainingNamespace.ToDisplayString(),
            new EquatableArray<ContainingTypeModel>(containingTypes),
            AccessibilityText(symbol.DeclaredAccessibility),
            symbol.Name,
            symbol.IsExtensionMethod,
            commandText!,
            rowType!.ToDisplayString(TypeTextFormat),
            new EquatableArray<ColumnModel>(columns),
            new EquatableArray<MethodParameterModel>([.. parameters]));
        return new SqlQueryPipelineResult(model, EquatableArray<DiagnosticInfo>.Empty);
    }

    private static ColumnModel[] ParseColumns(
        ITypeSymbol rowType,
        INamedTypeSymbol? guidType,
        Action<DiagnosticDescriptor, string[]> report)
    {
        var rowTypeText = rowType.ToDisplayString();

        if (rowType is not INamedTypeSymbol named
            || named.IsAbstract
            || named.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            report(SqlQueryDiagnostics.UnsupportedRowType, [rowTypeText]);
            return [];
        }

        var constructors = named.InstanceConstructors
            .Where(ctor => ctor.DeclaredAccessibility == Accessibility.Public && ctor.Parameters.Length > 0)
            .ToArray();
        if (constructors.Length != 1)
        {
            report(SqlQueryDiagnostics.UnsupportedRowType, [rowTypeText]);
            return [];
        }

        var columns = new List<ColumnModel>();
        foreach (var parameter in constructors[0].Parameters)
        {
            if (!TryGetGetter(parameter.Type, guidType, out var getter))
            {
                report(SqlQueryDiagnostics.UnsupportedRowType, [rowTypeText]);
                return [];
            }

            columns.Add(new ColumnModel(
                parameter.Name,
                parameter.Type.ToDisplayString(TypeTextFormat),
                getter!,
                IsNullable(parameter.Type)));
        }

        return [.. columns];
    }

    private static bool TryGetGetter(ITypeSymbol type, INamedTypeSymbol? guidType, out string? getter)
    {
        var underlying = StripNullable(type);
        getter = underlying.SpecialType switch
        {
            SpecialType.System_Int32 => "GetInt32",
            SpecialType.System_Int64 => "GetInt64",
            SpecialType.System_Int16 => "GetInt16",
            SpecialType.System_Byte => "GetByte",
            SpecialType.System_Boolean => "GetBoolean",
            SpecialType.System_String => "GetString",
            SpecialType.System_Double => "GetDouble",
            SpecialType.System_Single => "GetFloat",
            SpecialType.System_Decimal => "GetDecimal",
            SpecialType.System_DateTime => "GetDateTime",
            _ when SymbolEqualityComparer.Default.Equals(underlying, guidType) => "GetGuid",
            _ when underlying is IArrayTypeSymbol { IsSZArray: true, ElementType.SpecialType: SpecialType.System_Byte }
                => "GetFieldValue<byte[]>",
            _ => null,
        };
        return getter is not null;
    }

    private static bool IsNullable(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T }
        || (type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.Annotated);

    private static ITypeSymbol StripNullable(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;

    private static bool DerivesFrom(ITypeSymbol type, INamedTypeSymbol? baseType)
    {
        if (baseType is null)
        {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<INamedTypeSymbol> ContainingTypeChain(IMethodSymbol symbol)
    {
        for (var type = symbol.ContainingType; type is not null; type = type.ContainingType)
        {
            yield return type;
        }
    }

    private static string TypeText(ITypeSymbol type) => type.ToDisplayString(TypeTextFormat);

    private static string TypeKeyword(INamedTypeSymbol type) => type switch
    {
        { IsRecord: true, TypeKind: TypeKind.Struct } => "record struct",
        { IsRecord: true } => "record",
        { TypeKind: TypeKind.Struct } => "struct",
        { TypeKind: TypeKind.Interface } => "interface",
        _ => "class",
    };

    private static string AccessibilityText(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Internal => "internal",
        Accessibility.Protected => "protected",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        _ => "private",
    };
}
