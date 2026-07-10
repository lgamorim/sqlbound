# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

SqlBound is a planned .NET/C# library providing SQLx-equivalent functionality (Rust's SQLx): **compile-time verified SQL queries** checked against a real database schema, reflection-free row materialization via Roslyn source generators, and SQL-file migrations. It will be published to NuGet.

Follow the design constraints below; they are deliberate decisions, not suggestions.

## Core design constraints

**Dapper coexistence is a hard requirement.** Both libraries must coexist cleanly in the same project, on the same `DbConnection` and `DbTransaction`, with zero conflict. Concretely:

- Build on `System.Data.Common` primitives; never own or wrap connections.
- Do NOT define extension methods on `IDbConnection`/`DbConnection` named `Query*`, `Execute*`, or `ExecuteScalar*` — those are Dapper's and would cause ambiguity errors when both namespaces are imported. Use generated static partial methods (the primary API) or distinct verbs (`FetchAsync`, `RunAsync`) / an instance-based `SqlSession` for the dynamic surface.
- No global static configuration state (Dapper's `SqlMapper` pattern) — conversions are resolved at compile time by the generator.
- SqlBound targets static, build-time-known queries; Dapper remains the tool for runtime-composed SQL. Don't add features that chase Dapper's niche.

**Verification architecture (the SQLx `query!` equivalent):**

- `[SqlQuery("...")]` attributes on `static partial` methods; an incremental source generator emits straight-line `DbDataReader` code (`GetInt32`, `IsDBNull`, …) — no reflection, no IL emit, Native AOT and trimming compatible.
- Database round-trips (prepare/describe against `SQLBOUND_DATABASE_URL`) happen only in the CLI `prepare` step or an opt-in MSBuild task — never inside the Roslyn analyzer. The in-IDE analyzer validates only against committed JSON snapshots in `.sqlbound/` (SQLx's offline `.sqlx` directory equivalent). This split is the load-bearing DX decision; there is an ADR planned for it.
- Diagnostic IDs use the `SQLB###` prefix.
- SQL Server is the pilot provider; then SQLite, Postgres, MySQL.

**Package layout (planned):**

- `SqlBound` — runtime core, `net8.0`+, dependency-free.
- `SqlBound.Generators` — generator + analyzer, `netstandard2.0` (Roslyn requirement), packed into `analyzers/dotnet/cs`, never a runtime dependency.
- `SqlBound.SqlServer` / `.Sqlite` / `.Npgsql` / `.MySql` — per-provider introspection and type mapping.
- `SqlBound.Cli` — dotnet tool: `prepare`, `verify`, `migrate add/run/revert`, `database create/drop`.

## Roadmap

Work proceeds milestone by milestone (M1–M16) across six phases: Bedrock (skeleton/CI, execution core, Dapper-coexistence sample), Codegen (materialization, query shapes, AOT + benchmarks), Verification (SQL Server introspection, diagnostics, offline snapshots), Providers, Migrations & CLI, and Ship (API freeze, NuGet 1.0). The Dapper-coexistence sample project (M3) doubles as a permanent CI regression test. Codegen precedes verification because the generator defines the shapes the verifier checks.

## Conventions
@.claude/rules/coding-standards.md
@.claude/rules/architecture.md
@.claude/rules/design-principles.md
@.claude/rules/testing.md
@.claude/rules/workflow.md
