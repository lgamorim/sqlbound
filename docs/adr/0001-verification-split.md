# 1. Split compile-time SQL verification between the CLI/MSBuild task and the Roslyn analyzer

## Status

Accepted

## Context

SqlBound's core differentiator (its SQLx `query!`-macro equivalent) is verifying each
`[SqlQuery]`-annotated method's SQL against a real database schema at build time: preparing the
statement, describing its result columns and parameters, and comparing that metadata against the
method's declared C# signature.

That verification requires a live round-trip to a database. Roslyn analyzers, however, are
expected to be fast, deterministic, and side-effect-free: they run repeatedly as the developer
types, inside the IDE's live analysis loop, and are not guaranteed to execute in an environment
with network or database access. An analyzer that opens a database connection on every keystroke
would make the IDE unusably slow at best, and simply fail to load in environments without DB
connectivity (a fresh CI checkout, a teammate without local credentials) at worst.

We need database-backed verification without coupling it to the fast, always-on analyzer loop.

## Decision

Split the verification pipeline into two stages that never share a process boundary:

1. **Preparation (I/O-bound, explicit, opt-in).** A CLI command (`dotnet sqlbound prepare`) and an
   optional opt-in MSBuild task connect to a real database — via `SQLBOUND_DATABASE_URL` — prepare
   and describe every `[SqlQuery]` in the project, and write the resulting metadata (parameter
   types, column names, types, and nullability) as JSON snapshots to a `.sqlbound/` directory that
   is committed to source control. This is the only place a live database connection is opened.
2. **Verification (fast, deterministic, offline).** The Roslyn analyzer that runs inside the IDE
   and on every build reads only the committed `.sqlbound/` snapshots — it never opens a network
   or database connection itself. It compares each snapshot against the generated method's
   declared shape and reports mismatches as `SQLB###` diagnostics.

This mirrors SQLx's own offline mode (the `.sqlx` directory it commits for `cargo check` without a
live database).

## Consequences

**Positive**

- The IDE stays responsive: the analyzer's hot path never performs I/O.
- Builds and CI are deterministic and don't require database connectivity by default — only the
  explicit `prepare` step does.
- Teammates and CI agents without local database access can still build and get full verification
  against the last-committed snapshot.

**Negative / trade-offs**

- Introduces a two-step workflow: a developer who changes a query or the underlying schema must
  remember to re-run `prepare` before the analyzer will reflect it.
- Snapshots can go stale silently if `prepare` isn't re-run and nothing checks for drift.

**Follow-up**

- A `prepare --check` (or equivalent) mode that fails if regenerating snapshots would produce a
  diff is required to catch staleness in CI. This is scoped to M9 (offline mode), not this
  milestone.
