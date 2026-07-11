# Diagnostics

All SqlBound diagnostics use the `SQLB` prefix. IDs below 100 are **usage** diagnostics reported
by the source generator — they validate how the attributes are used and never need a database.
IDs from 101 are **verification** diagnostics reported by the analyzer — they compare a method's
declared signature against the committed `.sqlbound/` snapshot for its SQL (see ADR 0001/0003).

Severities are defaults; consumers can retune any of them via `.editorconfig`
(`dotnet_diagnostic.SQLB101.severity = error`).

## Usage (`SqlBound.Usage`, generator)

| ID | Severity | Meaning |
|----|----------|---------|
| SQLB001 | Error | `[SqlQuery]`/`[SqlExecute]` method must be a partial definition without a body |
| SQLB002 | Error | Method must be static |
| SQLB003 | Error | Method must take a `DbConnection` (or derived) first parameter |
| SQLB004 | Error | Unsupported `[SqlQuery]` return type |
| SQLB005 | Error | Row type has no supported mapping (constructor or settable properties) |
| SQLB006 | Error | Query parameter type is not supported |
| SQLB007 | Error | Command text must not be empty |
| SQLB008 | Error | Method must not be generic or nested in a generic type |
| SQLB009 | Error | `[SqlExecute]` method must return `Task` or `Task<int>` |
| SQLB010 | Error | A method cannot carry both `[SqlQuery]` and `[SqlExecute]` |

## Verification (`SqlBound.Verification`, analyzer)

Reported only once the project has opted in by committing `.sqlbound/` snapshots (ADR 0003); a
project with no snapshots hears nothing.

| ID | Severity | Meaning |
|----|----------|---------|
| SQLB101 | Warning | Query has no snapshot — run the prepare step |
| SQLB102 | Warning | Snapshot is unreadable or no longer matches the command text — re-run prepare |
| SQLB103 | Error | Statement produces no result set, but the method expects one |
| SQLB104 | Error | Result set has no column with a declared name |
| SQLB105 | Error | Column CLR type differs from the declaration |
| SQLB106 | Error | Database column is nullable but declared non-nullable (the safe converse is silent) |
| SQLB107 | Info | Result set returns columns the method never reads |
| SQLB108 | Error | Statement uses a parameter the method does not declare |
| SQLB109 | Warning | Method declares a scalar parameter the statement never uses |
| SQLB110 | Error | Parameter CLR type differs from the declaration |
| SQLB111 | Warning | `[SqlExecute]` statement returns a result set it discards |

Two comparison rules worth knowing:

- **Columns match by case-insensitive name, not position** — generated code binds with
  `GetOrdinal(name)`, so reordering a `SELECT` list is never a mismatch. Scalar-shaped methods
  verify the first column instead.
- **Types compare as mapped CLR types, not SQL type names** — SQL Server's suggested parameter
  types are inferences (a comparison against `decimal(18,2)` suggests `decimal(38,19)`), so raw
  SQL type comparison would false-positive.
