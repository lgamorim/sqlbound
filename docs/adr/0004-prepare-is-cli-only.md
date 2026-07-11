# 4. The prepare step is CLI-only; the opt-in MSBuild task is deferred

## Status

Accepted

## Context

ADR 0001 named two hosts for the database-describing prepare step: the CLI command
(`dotnet sqlbound prepare`) and "an optional opt-in MSBuild task", while flagging that the task
raises a build-ordering problem the CLI does not have: running inside the build, it would
regenerate snapshots *after* the analyzer had already read the old ones, so the build that
refreshed the snapshots would report diagnostics from the stale set (or require a second build
to converge).

M9 had to decide whether to ship the task alongside the CLI or hold it back.

## Decision

Prepare ships as a CLI command only. The MSBuild task is deferred — not dropped — until there is
a design that reads snapshots before compilation deterministically (e.g., a target scoped to run
before `CoreCompile` with correct incrementality, or an explicit two-build contract). Nothing in
the CLI-only shape forecloses adding the task later; it would reuse `PrepareRunner` unchanged.

The CI staleness gate this leaves open is covered by `prepare --check` (exit code 2 on drift),
which is the recommended pipeline step and does not have the ordering problem because it writes
nothing.

## Consequences

**Positive**

- No half-correct build integration: a build never observes snapshots mid-rewrite.
- One prepare implementation to maintain, exercised end-to-end by integration tests.

**Negative / trade-offs**

- Developers must run `dotnet sqlbound prepare` explicitly after changing a query or the schema;
  the IDE nudges them via SQLB101/SQLB102 (ADR 0003), and CI enforces via `prepare --check`.
