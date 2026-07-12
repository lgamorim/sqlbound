# SqlBound

SqlBound is a .NET library providing [SQLx](https://github.com/launchbadge/sqlx)-equivalent
functionality for C#: SQL queries verified at compile time against a real database schema,
reflection-free row materialization via a Roslyn incremental source generator, and clean
coexistence with [Dapper](https://github.com/DapperLib/Dapper) in the same project.

Status: **v0.3.0** — Phases 1–3 (Bedrock, Codegen, Verification) are complete. Phases 4–6
(Providers, Migrations & CLI, Ship) are still ahead; see [Roadmap](#roadmap).

## Why

Dapper maps hand-written, runtime-composed SQL with reflection. SqlBound targets the other
half of the problem: SQL that is *static and known at build time*. For that SQL, it generates
straight-line ADO.NET reader code ahead of time (no reflection, no IL emit — Native AOT and
trimming compatible) and verifies the query's shape against the database before it ever runs,
turning a class of runtime SQL errors into compiler diagnostics.

The two libraries are designed to coexist on the same `DbConnection`/`DbTransaction` with zero
conflict — SqlBound never owns a connection and never defines `Query*`/`Execute*` extension
methods that would collide with Dapper's.

## Quick start

```csharp
using System.Data.Common;

public static partial class ItemQueries
{
    [SqlQuery("SELECT id AS Id, name AS Name, price AS Price FROM items WHERE category = @category ORDER BY id")]
    public static partial Task<IReadOnlyList<Item>> GetByCategoryAsync(
        DbConnection connection, string category, CancellationToken cancellationToken = default);

    [SqlExecute("DELETE FROM items WHERE category = @category")]
    public static partial Task<int> DeleteByCategoryAsync(
        DbConnection connection, string category, CancellationToken cancellationToken = default);
}

public sealed record Item(int Id, string Name, decimal? Price);
```

The generator emits the method bodies. Result shapes are inferred from the return type:
`Task<T>` (single row, throws if not exactly one), `Task<T?>` (single row or none),
`Task<IReadOnlyList<T>>`, `IAsyncEnumerable<T>` (streaming), scalar types (`Task<int>`,
`Task<string>`, …), and `Task`/`Task<int>` for `[SqlExecute]` statements.

For SQL that isn't known until runtime — dynamic filters, user-composed queries — `SqlSession`
gives Dapper-style access (`RunAsync`, distinct verbs so the two APIs never collide) without a
generator in the loop.

## Compile-time verification

An opt-in, two-stage pipeline (mirroring SQLx's own offline `.sqlx` mode):

1. **`dotnet sqlbound prepare`** — an explicit CLI step that connects to a real database (via
   `SQLBOUND_DATABASE_URL`), describes every `[SqlQuery]`/`[SqlExecute]` statement's columns and
   parameters, and commits the result as JSON snapshots under `.sqlbound/`. This is the only
   place a live database connection is opened; it never happens during a normal build.
2. **The analyzer** reads only those committed snapshots — offline, on every build and in the
   IDE — and reports `SQLB101`–`SQLB111` for missing/stale snapshots and column, type, or
   parameter mismatches against the method's declared C# signature.

Projects that never run `prepare` never see a verification diagnostic (opt-in by snapshot
presence). See [docs/verification.md](docs/verification.md), [docs/diagnostics.md](docs/diagnostics.md),
and [docs/introspection.md](docs/introspection.md) for the full workflow, the diagnostic catalog,
and how SQL Server introspection works today.

## Migrations

Schema changes are ordered SQL-file migrations — paired `{version}_{name}.up.sql` / `.down.sql`
files with a timestamp version — tracked in a `_sqlbound_migrations` ledger. `dotnet sqlbound
migrate add` scaffolds a migration and `dotnet sqlbound database create`/`drop` manage the target
database. Applying and reverting migrations land in M14. See [docs/migrations.md](docs/migrations.md).

## Packages

| Package | Purpose |
|---|---|
| `SqlBound` | Runtime core — attributes, `SqlSession`, dependency-free. |
| `SqlBound.Generators` | The incremental source generator and the verification analyzer. Packed as an analyzer, never a runtime dependency. |
| `SqlBound.SqlServer` / `.Sqlite` / `.Npgsql` / `.MySql` | Per-provider introspection and SQL-to-CLR type mapping. SQL Server is the pilot; SQLite, Postgres, and MySQL shipped in Phase 4. |
| `SqlBound.Migrations` | Provider-neutral SQL-file migration model and the `IMigrationLedger` history contract. |
| `SqlBound.Cli` | The `sqlbound` dotnet tool — `prepare`, `migrate add`, and `database create`/`drop`. |

## Design decisions

Architectural decisions are recorded as ADRs in [docs/adr/](docs/adr/):

- [0001](docs/adr/0001-verification-split.md) — split verification between the CLI/MSBuild task (I/O) and the analyzer (offline)
- [0002](docs/adr/0002-generator-snapshot-consumption.md) — the generator emits from the declared signature alone; it never reads snapshots
- [0003](docs/adr/0003-verification-opt-in-by-snapshot-presence.md) — a project with no `.sqlbound/` snapshots gets no verification diagnostics
- [0004](docs/adr/0004-prepare-is-cli-only.md) — `prepare` stays a CLI step; an MSBuild task is deferred
- [0005](docs/adr/0005-sqlite-describe-scope.md) — SQLite describe stays dry-run-only; computed columns and parameter types are out of scope
- [0006](docs/adr/0006-migration-file-format.md) — migrations are paired up/down SQL files with a timestamp version and a checksummed ledger

## Performance

`bench/SqlBound.Benchmarks` compares generated code against Dapper and hand-written ADO.NET.
SqlBound stays within 1–9% of raw ADO.NET and beats Dapper on both time and allocations in every
category measured so far; see [docs/benchmarks.md](docs/benchmarks.md) for the methodology and
baseline numbers.

Native AOT and trimming compatibility is exercised on every CI run by
`test/SqlBound.AotSmokeTest`, a published-and-executed smoke test.

## Roadmap

Work proceeds milestone by milestone across six phases, each phase owning a semantic-versioning
minor version:

| Phase | Milestones | Status |
|---|---|---|
| 1 — Bedrock | M1–M3 | Done (`v0.1.0`) |
| 2 — Codegen | M4–M6 | Done (`v0.2.0`) |
| 3 — Verification | M7–M9 | Done (`v0.3.0`) |
| 4 — Providers | M10–M12 | Done (`v0.4.0`) |
| 5 — Migrations & CLI | M13– | In progress |
| 6 — Ship | — | Planned (`v1.0.0`) |

## License

[Apache-2.0](LICENSE)
