# SQL Server introspection

`SqlBound.SqlServer` provides the describe machinery behind the `prepare` step (ADR 0001): given
an open `SqlConnection` and a command text, `SqlServerQueryDescriber.DescribeAsync` returns a
`QueryDescription` — the result columns and parameters SQL Server reports for that SQL. This is
the metadata the offline `.sqlbound/` snapshots (M9) will serialize and the analyzer (M8) will
compare against each `[SqlQuery]`/`[SqlExecute]` method's declared signature.

Per ADR 0001, this round-trip runs only in the CLI `prepare` command or the opt-in MSBuild task —
never in the Roslyn analyzer and never at application runtime.

## Mechanism

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

## Known SQL Server limits

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

## Testing

Unit tests cover the type map exhaustively. Integration tests exercise the describer against a
real SQL Server 2022 started per test run via
[Testcontainers](https://dotnet.testcontainers.org/): with Docker present (locally or in CI) they
run for real; without Docker they skip locally with an explanatory message but fail hard in CI,
so a broken CI Docker setup cannot silently disable the suite.
