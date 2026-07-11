# 3. Verification is opted into by snapshot presence, and a missing snapshot is then a warning

## Status

Accepted

## Context

ADR 0001 deliberately left one question open: what should the analyzer report when a
`[SqlQuery]`/`[SqlExecute]` method has *no* `.sqlbound/` snapshot at all, as opposed to a
mismatched one? Under the signature-driven codegen chosen in ADR 0002 the generator does not need
snapshots, so a missing one is not inherently an error — the code still compiles and runs.

The tension is between two consumer populations:

- **Codegen-only consumers** adopt SqlBound for reflection-free materialization and may have no
  database reachable at build time. Nagging every query with an unfixable diagnostic (fatal under
  `TreatWarningsAsErrors`) would make the package hostile out of the box.
- **Verification adopters** run `prepare` and commit snapshots. For them, a query without a
  snapshot is precisely the failure mode ADR 0001 worries about: someone added or edited a query
  and forgot to re-run `prepare`, silently losing compile-time verification for that query.

A fixed severity cannot serve both: always-error breaks the first group, always-silent leaves the
second group's coverage hole open until CI runs `prepare --check` (M9).

## Decision

The committed `.sqlbound/` directory itself is the opt-in switch:

- **No `.sqlbound/` files wired as `AdditionalFiles` → the verification analyzer is fully
  silent.** A project that never runs `prepare` never hears about verification.
- **Any `.sqlbound/` file present → verification is on for the whole project.** Every query
  method without a matching snapshot reports `SQLB101` (warning), an unreadable or
  command-text-mismatched snapshot reports `SQLB102` (warning), and matched snapshots are
  compared normally (SQLB103–111).

"The team has opted into verification" is thereby a property of the repository — recorded by the
first committed snapshot — rather than of any per-project MSBuild flag that could drift between
projects and their snapshots.

Severities remain tunable per consumer via `.editorconfig`
(`dotnet_diagnostic.SQLB101.severity = error` for teams that want missing coverage to fail the
build).

## Consequences

**Positive**

- Zero-configuration adoption for codegen-only use; zero-configuration enforcement the moment the
  first snapshot is committed.
- The forgot-to-re-run-`prepare` hole is surfaced in the IDE immediately, not first in CI.

**Negative / trade-offs**

- Deleting the whole `.sqlbound/` directory silently turns verification off; only M9's
  `prepare --check` in CI can catch that. This is accepted — the same act is also the documented
  way to genuinely opt out.
- The first `prepare` run flips every unprepared query in the project to SQLB101 at once. This is
  the intended "now finish preparing" signal, but it can be noisy on large codebases adopting
  verification incrementally; teams can downgrade SQLB101 via `.editorconfig` during migration.
