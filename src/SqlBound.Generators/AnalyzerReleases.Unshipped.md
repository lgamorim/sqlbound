; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------------------------------------------------------------------
SQLB001 | SqlBound.Usage | Error | [SqlQuery] method must be a partial definition without a body
SQLB002 | SqlBound.Usage | Error | [SqlQuery] method must be static
SQLB003 | SqlBound.Usage | Error | [SqlQuery] method must take a DbConnection (or derived) first parameter
SQLB004 | SqlBound.Usage | Error | [SqlQuery] method must return Task&lt;IReadOnlyList&lt;T&gt;&gt;
SQLB005 | SqlBound.Usage | Error | Row type must have one public constructor with supported column types
SQLB006 | SqlBound.Usage | Error | Query parameter type is not supported
SQLB007 | SqlBound.Usage | Error | [SqlQuery] command text must not be empty
SQLB008 | SqlBound.Usage | Error | [SqlQuery] method must not be generic or nested in a generic type
