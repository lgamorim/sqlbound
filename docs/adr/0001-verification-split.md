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
would make the IDE unusably slow at best, and leave analysis failing or hanging in environments
without DB connectivity (a fresh CI checkout, a teammate without local credentials) at worst.

We need database-backed verification without coupling it to the fast, always-on analyzer loop.

## Decision

Split the verification pipeline into two stages that run in separate processes and communicate
only through committed snapshot files:

1. **Preparation (I/O-bound, explicit, opt-in).** A CLI command (`dotnet sqlbound prepare`) and an
   optional opt-in MSBuild task connect to a real database — via `SQLBOUND_DATABASE_URL` — prepare
   and describe every `[SqlQuery]` in the project, and write the resulting metadata (parameter
   types, column names, types, and nullability) as JSON snapshots to a `.sqlbound/` directory that
   is committed to source control. This is the only place a live database connection is opened.
2. **Verification (fast, deterministic, offline).** The Roslyn analyzer that runs inside the IDE
   and on every build reads only the committed `.sqlbound/` snapshots — it never opens a network
   or database connection itself. It compares each snapshot against the partial method's declared
   signature and reports mismatches as `SQLB###` diagnostics.

   The analyzer consumes the snapshots through Roslyn's `AdditionalFiles`/`AdditionalTexts`
   mechanism, wired up automatically for consumers via a `buildTransitive` `.props` file in the
   NuGet package. This is not an implementation detail but the enabling mechanism of the split:
   analyzers are prohibited from arbitrary file I/O (RS1035), and `AdditionalTexts` participate in
   Roslyn's change tracking, so editing a snapshot correctly re-triggers analysis.

**Deferred to ADR 0002:** whether the *source generator* also consumes the `.sqlbound/` snapshots
(snapshot-driven codegen, where a missing snapshot is a build error) or emits materialization code
purely from the declared C# method signature (signature-driven codegen, where snapshots feed only
the analyzer). This ADR constrains both options equally — no database I/O outside the prepare
step — but does not choose between them.

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
- The opt-in MSBuild `prepare` task raises a build-ordering question the CLI path does not have:
  it would regenerate snapshots mid-build, after the analyzer has already read the old ones. This
  must be resolved when the task is designed (e.g., by requiring a second build, or by scoping the
  task to run before compilation).

**Follow-up**

- A `prepare --check` (or equivalent) mode that fails if regenerating snapshots would produce a
  diff is required to catch staleness in CI. This is scoped to M9 (offline mode), not this
  milestone.
- Snapshot file granularity is one file per query (keyed by a content hash), not a single
  aggregate file. SQLx abandoned its original single `sqlx-data.json` for a per-query `.sqlx/`
  directory precisely because the aggregate file caused constant merge conflicts between parallel
  branches. The exact file layout and naming are finalized in M9.
- The analyzer's behavior when a query has *no* snapshot at all (as opposed to a mismatched one)
  is deliberately left open here and must be defined in M8's diagnostic design. It interacts with
  ADR 0002: under snapshot-driven codegen a missing snapshot is necessarily a build error, while
  under signature-driven codegen it could be anything from silence to a warning.
