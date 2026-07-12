# Database introspection

Each provider package supplies the describe machinery behind the `prepare` step (ADR 0001):
given an open connection and a command text, its `IQueryDescriber` implementation
(`SqlBound.Introspection`) returns a `QueryDescription` — the result columns and parameters the
database reports for that SQL. This is the metadata the offline `.sqlbound/` snapshots (M9)
serialize and the analyzer (M8) compares against each `[SqlQuery]`/`[SqlExecute]` method's
declared signature.

Per ADR 0001, this round-trip runs only in the CLI `prepare` command or the opt-in MSBuild task —
never in the Roslyn analyzer and never at application runtime.

## SQL Server (`SqlBound.SqlServer`)

### Mechanism

- **Result columns** come from `sp_describe_first_result_set`: name, zero-based ordinal
  (matching `DbDataReader` ordinals), the reported system type name (e.g. `decimal(18,2)`), and
  nullability. A statement with no result set (a bare `INSERT`/`UPDATE`/`DELETE`) describes as
  zero columns, which is exactly what `[SqlExecute]` methods expect.
- **Parameters** come from `sp_describe_undeclared_parameters`: each `@name` placeholder with the
  server's *suggested* type. Names are reported without the `@` so they compare directly against
  C# method parameter names.
- Both results map SQL types to C# type text through the same vocabulary the generator's getter
  set supports (`int`, `string`, `decimal`, `global::System.Guid`, `byte[]`, …). A type outside
  that set fails the describe with `SqlBoundDescribeException` rather than promising a
  materialization the generated code cannot perform.

### Known limits

These are inherent to `sp_describe_first_result_set` / `sp_describe_undeclared_parameters` and
surface as `SqlBoundDescribeException` with the server's own error text:

- **Temp tables**: statements that create and read `#temp` tables cannot be described.
- **Ambiguous parameters**: a placeholder used in contexts implying conflicting types
  (e.g. `Id = @p AND Name = @p`) fails parameter describe.
- **Suggested parameter types are inferences, not column lookups**: a comparison like
  `Price > @minPrice` against a `decimal(18,2)` column suggests the widened `decimal(38,19)`.
- **Unsupported types**: `datetimeoffset`, `time`, `xml`, `sql_variant`, spatial types, and CLR
  UDTs have no generator-supported reader getter yet, so columns and parameters of those types
  are rejected. (`datetimeoffset`/`time` support would first need `GetFieldValue`-based getters
  in the generator.)

### Testing

Unit tests cover the type map exhaustively. Integration tests exercise the describer against a
real SQL Server 2022 started per test run via
[Testcontainers](https://dotnet.testcontainers.org/): with Docker present (locally or in CI) they
run for real; without Docker they skip locally with an explanatory message but fail hard in CI,
so a broken CI Docker setup cannot silently disable the suite.

## SQLite (`SqlBound.Sqlite`)

SQLite has no describe-only RPC equivalent to `sp_describe_first_result_set` /
`sp_describe_undeclared_parameters`, and `Microsoft.Data.Sqlite`'s own API surface can't fill the
gap either (`SqliteCommand` has no undeclared-parameter discovery, and `CommandBehavior.SchemaOnly`
rejects a statement with an unbound named parameter). `SqliteQueryDescriber` instead talks to the
connection's raw `sqlite3*` handle directly (`SQLitePCLRaw.raw`):

### Mechanism

- **Compilation** uses `sqlite3_prepare_v2`, which — like SQL Server's `sp_describe_*` — only
  compiles the statement; it never executes it, so describing an `[SqlExecute]` `DELETE`/`UPDATE`
  is as safe as describing a `SELECT`.
- **Result columns** come from `sqlite3_column_decltype` (the type declared in `CREATE TABLE` for
  that column) and `sqlite3_table_column_metadata` (nullability, via the column's `NOT NULL`
  constraint, resolved through `sqlite3_column_table_name`/`sqlite3_column_origin_name`).
- **Parameters** come from `sqlite3_bind_parameter_name`/`sqlite3_bind_parameter_count`: only a
  name, stripped of its marker character (`@`/`:`/`$`) to match the C# parameter it binds to.

### Known limits

Both stem from SQLite's dynamic typing, not from a gap in this implementation — see
[ADR 0005](adr/0005-sqlite-describe-scope.md) for the full reasoning:

- **No parameter typing.** SQLite has no static parameter typing at all — there is nothing
  equivalent to SQL Server's *suggested* parameter type to report. A SQLite-described parameter's
  snapshot carries a `null` `clrTypeText`; the analyzer's `SQLB110` (parameter type mismatch) has
  nothing to compare against and stays silent for that parameter. `SQLB108`/`SQLB109` (unknown /
  unused parameter) still work off names alone.
- **No computed-expression columns.** `sqlite3_column_decltype` returns nothing for anything that
  isn't a direct table column reference — `COUNT(*)`, arithmetic, `CAST`, and other function
  results all describe with no declared type. Rather than executing the statement to guess a type
  from a runtime value (unsafe for `[SqlExecute]`, and unreliable when the query returns no rows),
  `SqliteQueryDescriber` throws `SqlBoundDescribeException` for such a column. A query that needs
  a computed column today has to be described some other way, or reworked to select only direct
  columns.
- **Positional (`?`) parameters are rejected.** SqlBound binds parameters by name; a placeholder
  with no name can't be correlated to a C# method parameter.

### Testing

Unit tests cover the type map exhaustively. Integration tests exercise the describer against a
real, embedded SQLite database — no container needed. Each test opens its own connection to a
named, shared-cache in-memory database (`Mode=Memory;Cache=Shared`) seeded once per test assembly,
because `sqlite3_errmsg` reflects the last operation on a connection handle: sharing one open
connection across parallel tests would let them observe each other's errors.
