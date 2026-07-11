using Microsoft.CodeAnalysis;

namespace SqlBound.Generators;

/// <summary>The <c>SQLB0##</c> descriptors for invalid <c>[SqlQuery]</c> usage reported by the generator.</summary>
internal static class SqlQueryDiagnostics
{
    private const string Category = "SqlBound.Usage";

    public static readonly DiagnosticDescriptor MethodMustBePartialDefinition = new(
        "SQLB001",
        "Method must be a partial definition",
        "Method '{0}' marked with [{1}] must be a partial method definition with no body and no separate implementation part",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MethodMustBeStatic = new(
        "SQLB002",
        "Method must be static",
        "Method '{0}' marked with [{1}] must be static",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MethodMustTakeDbConnectionFirst = new(
        "SQLB003",
        "Method must take a DbConnection first",
        "Method '{0}' marked with [{1}] must declare a System.Data.Common.DbConnection (or derived) first parameter",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedReturnType = new(
        "SQLB004",
        "Unsupported return type",
        "Method '{0}' returns '{1}' but [SqlQuery] methods must return Task<T>, Task<T?>, Task<IReadOnlyList<T>>, or IAsyncEnumerable<T> where T is a supported row or scalar type",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedRowType = new(
        "SQLB005",
        "Unsupported row type",
        "Row type '{0}' must map columns through exactly one public constructor with parameters, or through public settable properties on a parameterless-constructible type, and every mapped member must be of a supported column type",
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
        "The command text must not be null, empty, or whitespace",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor GenericDeclarationsNotSupported = new(
        "SQLB008",
        "Generic declarations are not supported",
        "Method '{0}' marked with [{1}] must not be generic or declared within a generic type",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedExecuteReturnType = new(
        "SQLB009",
        "Unsupported execute return type",
        "Method '{0}' returns '{1}' but [SqlExecute] methods must return Task (discarding the count) or Task<int> (the number of affected rows)",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MethodMustNotCarryBothAttributes = new(
        "SQLB010",
        "Method must not carry both attributes",
        "Method '{0}' cannot be marked with both [SqlQuery] and [SqlExecute]",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
