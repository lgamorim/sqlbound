# SqlBound — project-specific rules

Rules that apply only to this repository, composed on top of the shared
`library-team` profile. Shared modules under `core/`, `archetype/`, and
`overlays/` are copied verbatim from the `claude-rules` repo and must stay
byte-identical so `tools/sync.ps1 -Check` audits cleanly — never edit them
here. Sqlbound-specific conventions land in this file; corrections to a shared
rule go upstream to `claude-rules` first and are then re-synced.

## Workflow — milestones, phases, versioning

- For each milestone, draft a plan first and present it to the user; execution
  starts only after the user approves the plan.
- Milestones (M1–M16) are used to track the progress of the project. All
  milestone work happens on a `feature/M<number>-<desc>`-prefixed branch.
- Versioning follows semantic versioning: each phase gets its own minor version
  (Phase 1 → `0.1.x`, Phase 2 → `0.2.x`, …; Phase 6 ships `1.0.0`).
- When a phase completes, tag it on the default branch with an annotated tag
  (e.g., `git tag -a v0.1.0 -m "..."`) and push the tag to GitHub for
  reference.
- `PackageVersion` carries a prerelease suffix (`X.Y.0-preview.N`) during a
  phase's active development. Closing the phase drops the suffix to the clean
  `X.Y.0` in the same commit that gets tagged, so the tag always matches the
  package version it marks exactly. The next phase's first commit starts the
  new prerelease line (`X.(Y+1).0-preview.1`).
- Each phase has one matching GitHub milestone (titled
  `Phase N — <Name> (0.Y.x)`), not one per M-number; every M-number's PR in
  that phase is associated with the phase's milestone on creation, and the
  milestone is closed when the phase's final PR merges. The milestone's
  description lists each composing M-number with its own description as a
  bullet, so the phase-level summary and the per-milestone detail both stay
  visible in one place.

## Testing

- Generator output is snapshot-tested with the Roslyn testing SDK.
- Benchmarks compare against Dapper and raw ADO.NET (the `bench/` layout and
  CI policy come from the shared benchmarks overlay).

## Architecture

- The shared "one solution file (`.slnx`) per repo, at its root" rule extends
  here to "per repo or example": a self-contained example project gets its own
  `.slnx` at its own root.
