# 5. SQLite describe stays dry-run-only; computed columns and parameter types are out of scope

## Status

Accepted

## Context

M10 added `SqlBound.Sqlite` as the second introspection provider, which is what forced
[the provider-neutral `IQueryDescriber` abstraction](../../src/SqlBound.Introspection) out of the
SQL-Server-only design M7 shipped. Reaching SQL Server's describe fidelity with SQLite turned out
not to be possible with an equivalently safe mechanism, for two independent reasons:

1. **SQLite has no static parameter typing.** `sp_describe_undeclared_parameters` has no SQLite
   equivalent — there is no server-side type inference to query, because SQLite parameters simply
   don't carry a declared type at the engine level.
2. **`sqlite3_column_decltype` only resolves direct table column references.** For a computed
   expression, function call, or aggregate (`COUNT(*)`, arithmetic, `CAST`), it returns nothing —
   there is no declared type to report, because the SQL Server equivalent (a *suggested* type
   inferred from the expression) doesn't exist as a queryable property of a SQLite prepared
   statement.

For (2), a workaround exists in principle: `sqlite3_prepare_v2` never executes the statement (a
`DELETE` compiles without deleting anything), so it would be possible to fall back to actually
*running* the statement once — binding every parameter to `NULL` — and read
`sqlite3_column_type()` on the resulting row for a computed column's runtime type. Two problems
rule this out for the current milestone:

- **It is unsafe for `[SqlExecute]` statements with a `RETURNING` clause.** Unlike
  `sqlite3_prepare_v2`, `sqlite3_step()` genuinely executes the statement. A `DELETE ... RETURNING`
  with a computed `RETURNING` column would delete real rows during `prepare` — the exact hazard
  ADR 0001 designed the CLI-only split to avoid.
- **It is unreliable even when safe.** A `SELECT` with a highly selective `WHERE` clause commonly
  returns zero rows when every parameter is bound to `NULL`, leaving no row to inspect and no
  fallback for the fallback.

## Decision

`SqliteQueryDescriber` stays strictly dry-run, matching SQL Server's safety model exactly:

- A parameter's `ClrTypeText` is `null` in the snapshot (`SqlBound.Introspection.DescribedParameter`
  and the JSON schema both allow this). The analyzer's `SQLB110` (parameter type mismatch) has
  nothing to compare against and is skipped for that parameter; `SQLB108`/`SQLB109` still work off
  parameter names alone.
- A column whose `sqlite3_column_decltype` is empty throws `SqlBoundDescribeException` with a
  message naming the column and explaining why (it is a computed expression, not a missing
  feature). The query must be restructured to select only direct table columns, or described some
  other way, until a future milestone (if ever) adds an execute-based fallback gated on
  `sqlite3_stmt_readonly()`.

## Consequences

**Positive**

- No new unsafe code path: `prepare` against SQLite can never execute a statement that mutates
  data, exactly like `prepare` against SQL Server.
- The limitation is explicit and fails loudly at `prepare` time with an actionable message, rather
  than silently guessing a type that might be wrong.

**Negative / trade-offs**

- `COUNT(*)`/scalar-aggregate queries and any query with a computed `SELECT` column cannot be
  verified against SQLite today. This is a real gap relative to SQL Server's provider.
- `SQLB110` can never fire for a SQLite-sourced parameter — a parameter's C# type is effectively
  unverified for SQLite, trusted from the method's own declared signature.

**Follow-up**

- An execute-based fallback for computed columns, gated on `sqlite3_stmt_readonly()` and
  documented as a distinct, less-safe opt-in, is a candidate for a future milestone if this
  limitation proves painful in practice.
