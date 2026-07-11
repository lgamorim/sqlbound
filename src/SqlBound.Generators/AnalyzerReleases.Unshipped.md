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
SQLB008 | SqlBound.Usage | Error | [SqlQuery]/[SqlExecute] method must not be generic or nested in a generic type
SQLB009 | SqlBound.Usage | Error | [SqlExecute] method must return Task or Task&lt;int&gt;
SQLB010 | SqlBound.Usage | Error | A method cannot carry both [SqlQuery] and [SqlExecute]
SQLB101 | SqlBound.Verification | Warning | Query has no .sqlbound snapshot (reported only once verification is opted in)
SQLB102 | SqlBound.Verification | Warning | Snapshot file is unreadable or stale
SQLB103 | SqlBound.Verification | Error | Statement produces no result set but the method expects one
SQLB104 | SqlBound.Verification | Error | Result set has no column with the declared name
SQLB105 | SqlBound.Verification | Error | Column CLR type differs from the declaration
SQLB106 | SqlBound.Verification | Error | Database column is nullable but declared non-nullable
SQLB107 | SqlBound.Verification | Info | Result set returns columns the method never reads
SQLB108 | SqlBound.Verification | Error | Statement uses a parameter the method does not declare
SQLB109 | SqlBound.Verification | Warning | Method declares a scalar parameter the statement never uses
SQLB110 | SqlBound.Verification | Error | Parameter CLR type differs from the declaration
SQLB111 | SqlBound.Verification | Warning | [SqlExecute] statement returns a result set it discards
