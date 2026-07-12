<p align="center">
  <img src="assets/logo.png" alt="SqlBound" width="280">
</p>

# SqlBound

SqlBound is a .NET library providing [SQLx](https://github.com/launchbadge/sqlx)-equivalent
functionality for C#: SQL queries verified at compile time against a real database schema,
reflection-free row materialization via a Roslyn incremental source generator, SQL-file
migrations, and clean coexistence with [Dapper](https://github.com/DapperLib/Dapper) in the same
project.

Status: **v1.0.0-rc.1** — release candidate under review. Phases 1–5 (Bedrock, Codegen, Verification,
Providers, Migrations & CLI) are complete, and Phase 6 (Ship — API freeze and the 1.0 release) is in
progress; the stable `1.0.0` follows once the candidate is validated. See [Roadmap](#roadmap) and the
[changelog](CHANGELOG.md).

## Why

Dapper maps hand-written, runtime-composed SQL with reflection. SqlBound targets the other
half of the problem: SQL that is *static and known at build time*. For that SQL, it generates
straight-line ADO.NET reader code ahead of time (no reflection, no IL emit — Native AOT and
trimming compatible) and verifies the query's shape against the database before it ever runs,
turning a class of runtime SQL errors into compiler diagnostics.

The two libraries are designed to coexist on the same `DbConnection`/`DbTransaction` with zero
conflict — SqlBound never owns a connection and never defines `Query*`/`Execute*` extension
methods that would collide with Dapper's. See
[docs/dapper-coexistence.md](docs/dapper-coexistence.md) for a side-by-side guide.

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
and what each provider can and cannot describe.

## Migrations

Schema changes are ordered SQL-file migrations — paired `{version}_{name}.up.sql` / `.down.sql`
files with a timestamp version (the down file optional per migration) — tracked in a
`_sqlbound_migrations` ledger that checksums each applied script. The `sqlbound` tool drives the
whole lifecycle:

```bash
dotnet sqlbound migrate add "create items"   # scaffold a timestamped up/down pair
dotnet sqlbound migrate run                   # apply every pending migration, in order
dotnet sqlbound migrate status                # applied / pending / drifted / missing
dotnet sqlbound migrate revert                # roll back the most recent migration
dotnet sqlbound database create               # create or drop the target database
```

`migrate run` applies each migration in its own transaction (where the provider supports it),
refusing to proceed on checksum drift or an out-of-order migration. See
[docs/migrations.md](docs/migrations.md) for the file format, the ledger, and per-provider
behaviour. Design decisions are in [ADR 0006](docs/adr/0006-migration-file-format.md) and
[ADR 0007](docs/adr/0007-mysql-migrations-not-transactional.md).

## Providers

SQL Server is the pilot; SQLite, PostgreSQL, and MySQL followed. The provider is selected from the
`SQLBOUND_DATABASE_URL` scheme (`sqlserver://`, `sqlite://`, `postgresql://`, `mysql://`).

| Provider | Verification (`prepare`) | Migrations & `database` | Notes |
| --- | --- | --- | --- |
| SQL Server | Full (columns + parameters) | Yes, transactional | Pilot provider |
| PostgreSQL | Full (columns + parameters) | Yes, transactional | |
| SQLite | Columns only; computed columns rejected ([ADR 0005](docs/adr/0005-sqlite-describe-scope.md)) | Yes, transactional | The `Data Source` file is the database |
| MySQL | Columns only; no parameter typing | Yes, **not** transactional ([ADR 0007](docs/adr/0007-mysql-migrations-not-transactional.md)) | DDL auto-commits |

## Packages

| Package | Purpose |
|---|---|
| `SqlBound` | Runtime core — attributes, `SqlSession`, dependency-free. |
| `SqlBound.Generators` | The incremental source generator and the verification analyzer. Packed as an analyzer, never a runtime dependency. |
| `SqlBound.Introspection` | Provider-neutral introspection contracts (`IQueryDescriber`). |
| `SqlBound.Migrations` | Provider-neutral migration model, the `IMigrationLedger` history contract, and `IDatabaseAdmin`. |
| `SqlBound.SqlServer` / `.Sqlite` / `.Npgsql` / `.MySql` | Per-provider introspection, type mapping, migration ledger, and database administration. |
| `SqlBound.Cli` | The `sqlbound` dotnet tool — `prepare`, `migrate add`/`run`/`revert`/`status`, and `database create`/`drop`. |

## Design decisions

Architectural decisions are recorded as ADRs in [docs/adr/](docs/adr/):

- [0001](docs/adr/0001-verification-split.md) — split verification between the CLI/MSBuild task (I/O) and the analyzer (offline)
- [0002](docs/adr/0002-generator-snapshot-consumption.md) — the generator emits from the declared signature alone; it never reads snapshots
- [0003](docs/adr/0003-verification-opt-in-by-snapshot-presence.md) — a project with no `.sqlbound/` snapshots gets no verification diagnostics
- [0004](docs/adr/0004-prepare-is-cli-only.md) — `prepare` stays a CLI step; an MSBuild task is deferred
- [0005](docs/adr/0005-sqlite-describe-scope.md) — SQLite describe stays dry-run-only; computed columns and parameter types are out of scope
- [0006](docs/adr/0006-migration-file-format.md) — migrations are paired up/down SQL files with a timestamp version and a checksummed ledger
- [0007](docs/adr/0007-mysql-migrations-not-transactional.md) — MySQL migrations are not transactional, because MySQL commits DDL implicitly
- [0008](docs/adr/0008-api-stability-and-shipping.md) — the 1.0 public-API stability commitment, strong-naming, package signing, and the documentation site

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
| 5 — Migrations & CLI | M13–M15 | Done (`v0.5.0`) |
| 6 — Ship | M16 | Release candidate (`v1.0.0-rc.1`) |

## License

Copyright 2026 Luís Amorim. Licensed under the [Apache License 2.0](LICENSE).
