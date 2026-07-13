# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0-rc.1] - 2026-07-12

First release candidate for 1.0 (Phase 6 — Ship, M16). Freezes the public API
and finalizes packaging ahead of the stable 1.0.0 release. No functional
changes to the query, verification, or migration engines from `0.5.0` beyond
the MySQL scanner fix below.

### Added

- Strong-named assemblies across the whole library set, sharing one committed
  key so strong-named consumers can reference SqlBound.
- Public-API baselines (`PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`)
  on every shipping library, enforced at build time by
  `Microsoft.CodeAnalysis.PublicApiAnalyzers`, so post-1.0 surface changes are
  caught mechanically.
- A package-oriented NuGet README embedded in every package, plus per-package
  `PackageTags`.
- `ContinuousIntegrationBuild` for deterministic, path-normalized CI output;
  SourceLink (SDK-provided) verified to map symbols to the committed sources.
- Guide: "Using SqlBound alongside Dapper" (`docs/dapper-coexistence.md`).
- ADR 0008: public-API stability policy, package-signing rationale, and the
  documentation-site decision.
- A DocFX documentation site published to GitHub Pages.
- A tag-triggered NuGet release workflow (publish deliberately gated off until
  the 1.0.0 promotion, and additionally guarded by a required
  `expected-version` input that must match the packed output).

### Fixed

- The MySQL parameter scanner no longer mistakes `@@` system variables (e.g.
  `@@sql_mode`, `@@session.sql_mode`) for parameter placeholders, which made
  `prepare` declare a bogus parameter and verification demand a method
  parameter that should not exist.
- The MySQL parameter scanner now applies MySQL's actual line-comment rule:
  `--` starts a comment only when followed by whitespace or the end of the
  statement, so an expression like `1--@x` no longer swallows the rest of the
  line.
- `SqlSession.FetchScalarAsync<T>` reports a failed scalar conversion as an
  `InvalidOperationException` naming the actual and requested types (instead
  of leaking a bare `InvalidCastException`), and its no-value error message
  now acknowledges that the result may have been a database NULL.

### Changed

- Narrowed the `SqlBound.Migrations` public surface: `MigrationPlan`,
  `MigrationReverter`, and `MigrationStatusReport` are now `internal` (pure
  decision helpers composed by `MigrationRunner`; widening later is
  non-breaking, narrowing after 1.0 would not be).

## [0.5.0] - 2026-07-12

Phase 5 — Migrations & CLI (M13–M15).

### Added

- SQL-file migration model (paired `{version}_{name}.up.sql` / `.down.sql`,
  timestamp versions, SHA-256 checksums) and the `_sqlbound_migrations` ledger.
- Provider-neutral migration engine (`migrate add` / `run` / `revert` /
  `status`) with checksum-drift and out-of-order safety checks, plus per-migration
  transactions where the provider supports transactional DDL.
- `database create` / `drop` CLI commands.
- Cross-provider support for migrations and database administration across SQL
  Server, SQLite, PostgreSQL, and MySQL.
- ADR 0006 (migration file format) and ADR 0007 (MySQL non-transactional DDL).

## [0.4.0] - 2026-07-12

Phase 4 — Providers (M10–M12).

### Added

- Introspection providers for SQLite, PostgreSQL, and MySQL alongside SQL
  Server, each with real type mapping and describe fidelity verified against a
  live database.
- The `SqlBound.Introspection` abstraction (`IQueryDescriber`) that all
  providers implement.

## [0.3.0] - 2026-07-11

Phase 3 — Verification (M7–M9).

### Added

- SQL Server schema introspection, `SQLB###` diagnostics, and committed offline
  JSON snapshots (`.sqlbound/`) so the in-IDE analyzer validates without a
  database connection.

## [0.2.0] - 2026-07-11

Phase 2 — Codegen (M4–M6).

### Added

- Reflection-free row materialization via an incremental source generator,
  query-shape support, Native AOT compatibility, and benchmarks versus Dapper
  and raw ADO.NET.

## [0.1.0] - 2026-07-11

Phase 1 — Bedrock (M1–M3).

### Added

- Solution skeleton and CI, the `System.Data.Common`-based execution core, and
  the Dapper-coexistence sample that doubles as a permanent CI regression test.

[Unreleased]: https://github.com/lgamorim/sqlbound/compare/v1.0.0-rc.1...HEAD
[1.0.0-rc.1]: https://github.com/lgamorim/sqlbound/compare/v0.5.0...v1.0.0-rc.1
[0.5.0]: https://github.com/lgamorim/sqlbound/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/lgamorim/sqlbound/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/lgamorim/sqlbound/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/lgamorim/sqlbound/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/lgamorim/sqlbound/releases/tag/v0.1.0
