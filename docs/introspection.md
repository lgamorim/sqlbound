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

## PostgreSQL (`SqlBound.Npgsql`)

Postgres's wire protocol has a native, describe-only operation the other providers can only
approximate: the extended query protocol's `Describe` message, exposed through Npgsql as
`CommandBehavior.SchemaOnly`. It never executes the statement — like SQL Server's `sp_describe_*`
and SQLite's `sqlite3_prepare_v2` — but unlike SQLite's `sqlite3_column_decltype`, it resolves a
real type for *every* result column, including computed and aggregate ones, because Postgres's
planner resolves an expression's result type as part of parsing, not by inspecting a stored
`CREATE TABLE` declaration.

### Mechanism

- **Parameters** come from `NpgsqlCommandBuilder.DeriveParameters`, which infers a real type per
  placeholder from how it's used — the closest of the three providers to SQL Server's
  `sp_describe_undeclared_parameters`. It also reliably primes Npgsql's `@name` → `$N` rewriter,
  which is why the describer derives parameters before describing columns even for a statement
  with no placeholders.
- **Result columns** come from `CommandBehavior.SchemaOnly`'s column schema
  (`NpgsqlDbColumn.DataTypeName`), which resolves for computed and aggregate columns as well as
  direct table references.
- **Nullability** needs one further step `SchemaOnly` doesn't provide directly: for a column that
  is a direct table reference, `SchemaOnly` still reports the source table's OID and the column's
  attribute number (straight from the protocol's `RowDescription`, no execution needed), which one
  `SELECT attnotnull FROM pg_attribute WHERE attrelid = @tableOid AND attnum = @attNum` resolves
  into a real nullability flag. A computed/aggregate column has no source table to look up and
  defaults to nullable — the same safe-direction convention SQLite's provider uses for the columns
  it can describe at all.

### Known limits

- **`timestamp with time zone`, `time`, `interval`, `json`/`jsonb`, and array types are
  unmapped** — no generator-supported reader getter exists for them yet, the same rationale as SQL
  Server's `datetimeoffset`/`xml` rejection.
- **Ambiguous parameter usage fails describe**, same as SQL Server: a placeholder compared against
  incompatible column types (e.g. `id = @p OR name = @p`) fails during `DeriveParameters` with a
  `SqlBoundDescribeException`.
- Unlike SQLite, **no columns need to be rejected as undescribable** — the `Describe` message's
  planner-level type resolution covers `COUNT(*)`, arithmetic, `CAST`, and other expressions that
  SQLite's purely syntactic `decltype` cannot.

### Testing

Unit tests cover the type map exhaustively. Integration tests exercise the describer against a
real Postgres 16 started per test run via [Testcontainers](https://dotnet.testcontainers.org/),
same skip-locally/fail-in-CI policy as the SQL Server suite. Npgsql has no MARS (multiple
concurrent operations on one connection); the nullability lookup's own command only runs after the
`SchemaOnly` reader from the column describe step is fully disposed.

## MySQL (`SqlBound.MySql`)

MySQL's binary protocol has `COM_STMT_PREPARE`, exposed through MySqlConnector as
`MySqlCommand.PrepareAsync` plus `CommandBehavior.SchemaOnly` — like the other three providers,
it never executes the statement. Column fidelity matches Postgres (a real type, including for
computed and aggregate columns, plus nullability with no extra round-trip); parameter fidelity
matches SQLite (no real type at all).

### Mechanism

- **Parameters must be pre-declared before MySQL will describe anything, and MySQL has no
  server-side way to discover their names.** `MySqlParameterScanner` finds `@name` tokens in the
  command text by hand — skipping quoted string literals (both doubled-quote and backslash
  escaping), backtick-quoted identifiers, and `--`/`#`/`/* */` comments, so a literal like
  `'user@example.com'` is never mistaken for a placeholder — and the describer declares each with
  an arbitrary placeholder type (`MySqlDbType.VarChar`) purely to satisfy `SchemaOnly`'s
  precondition. The declared type has no effect on the result: MySQL's `SchemaOnly` describe
  resolves real result-column types from the query itself, confirmed empirically by declaring
  every parameter with a deliberately wrong type and observing correct column output.
- **Result columns** come from `CommandBehavior.SchemaOnly`'s column schema (`DataTypeName` and
  `AllowDBNull`), which resolves for computed and aggregate columns as well as direct table
  references — MySQL's query planner needs to know each result column's type to answer
  `COM_STMT_PREPARE` at all, the same reason Postgres can do this and SQLite cannot.
- **Nullability** comes directly from the column schema's `AllowDBNull` flag — no extra catalog
  round-trip needed, simpler than Postgres.

### Known limits

- **No parameter typing at all.** MySQL's prepared-statement protocol just echoes back whatever
  type the caller declares; there is nothing genuine to report. A MySQL-described parameter's
  snapshot carries a `null` `clrTypeText`, exactly like SQLite — `SQLB110` stays silent for it,
  while `SQLB108`/`SQLB109` still work off names alone.
- **`TIME`, `YEAR`, `BIT`, `JSON`, `ENUM`, and `SET` are unmapped** — no generator-supported reader
  getter exists for them yet, the same rationale as SQL Server's `datetimeoffset`/`xml` rejection.
- **A `BOOLEAN`-declared column is distinguishable from a genuine `TINYINT`** — MySQL itself has no
  native boolean storage type (`BOOLEAN` is an alias for `TINYINT(1)`), but MySqlConnector's schema
  already reports a `BOOLEAN`-declared column as `BOOL` rather than `TINYINT`, so no length-based
  heuristic is needed to tell them apart.

### Testing

Unit tests cover the type map and the parameter scanner exhaustively. Integration tests exercise
the describer against a real MySQL 8.4 started per test run via
[Testcontainers](https://dotnet.testcontainers.org/), same skip-locally/fail-in-CI policy as the
other three suites.
