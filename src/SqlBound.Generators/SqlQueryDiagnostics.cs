using Microsoft.CodeAnalysis;

namespace SqlBound.Generators;

/// <summary>The <c>SQLB0##</c> descriptors for invalid <c>[SqlQuery]</c> usage reported by the generator.</summary>
internal static class SqlQueryDiagnostics
{
    private const string Category = "SqlBound.Usage";

    public static readonly DiagnosticDescriptor MethodMustBePartialDefinition = new(
        "SQLB001",
        "Method must be a partial definition",
        "Method '{0}' marked with [SqlQuery] must be a partial method definition with no body and no separate implementation part",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MethodMustBeStatic = new(
        "SQLB002",
        "Method must be static",
        "Method '{0}' marked with [SqlQuery] must be static",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MethodMustTakeDbConnectionFirst = new(
        "SQLB003",
        "Method must take a DbConnection first",
        "Method '{0}' marked with [SqlQuery] must declare a System.Data.Common.DbConnection (or derived) first parameter",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedReturnType = new(
        "SQLB004",
        "Unsupported return type",
        "Method '{0}' returns '{1}' but [SqlQuery] methods must return Task<T>, Task<T?>, or Task<IReadOnlyList<T>> where T is a supported row or scalar type",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedRowType = new(
        "SQLB005",
        "Unsupported row type",
        "Row type '{0}' must expose exactly one public constructor with at least one parameter, and every constructor parameter must be of a supported column type",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedQueryParameterType = new(
        "SQLB006",
        "Unsupported query parameter type",
        "Parameter '{0}' of type '{1}' is not supported; query parameters must be supported scalar types, with an optional DbTransaction second parameter and an optional trailing CancellationToken",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CommandTextMustNotBeEmpty = new(
        "SQLB007",
        "Command text must not be empty",
        "The [SqlQuery] command text must not be null, empty, or whitespace",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor GenericDeclarationsNotSupported = new(
        "SQLB008",
        "Generic declarations are not supported",
        "Method '{0}' marked with [SqlQuery] must not be generic or declared within a generic type",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
