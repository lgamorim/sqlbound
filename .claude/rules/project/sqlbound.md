# SqlBound — project-specific rules

Rules that apply only to this repository, composed on top of the shared
`library-team` profile. Shared modules under `core/`, `archetype/`, and
`overlays/`, plus the profile manifest under `profiles/`, are copied
verbatim from the `claude-rules` repo and must stay byte-identical so
`tools/sync.ps1 -Check` audits cleanly — never edit them here.
Sqlbound-specific conventions land in this file; corrections to a shared
rule go upstream to `claude-rules` first and are then re-synced.

## Workflow — milestones, phases, versioning

- For each milestone, draft a plan first and present it to the user; execution
  starts only after the user approves the plan.
- Milestones (M1–M16) are used to track the progress of the project. All
  milestone work happens on a `feature/M<number>-<desc>`-prefixed branch.
- Versioning follows semantic versioning: each phase gets its own version line
  (Phase 1 → `0.1.x`, Phase 2 → `0.2.x`, …; Phase 6 ships `1.0.0`).
- Tag the default branch with an annotated tag (e.g.,
  `git tag -a v0.1.0 -m "..."`) when a phase completes and for each release
  candidate cut along the way, then push the tag to GitHub for reference.
- `PackageVersion` in `Directory.Build.props` carries a prerelease suffix
  while a phase is in flight: `X.Y.Z-preview.N` during development, and
  `X.Y.Z-rc.N` once the phase is tracking a stable release. Closing the phase
  drops the suffix to the clean `X.Y.Z` in the same commit that gets tagged.
  Every tag — release candidates included — is cut on a commit whose
  `PackageVersion` matches the tag name minus the leading `v`, so a tag always
  names its package version exactly. The next phase opens at the version the
  roadmap assigns it, not at a mechanical bump of the previous minor.
- A release candidate does not close its phase: `PackageVersion` moves to
  `X.Y.Z-rc.N`, `CHANGELOG.md` gets its own `## [X.Y.Z-rc.N]` entry, and the
  candidate is published to nuget.org as a prerelease so it can be validated
  by real consumers (NuGet offers prereleases only to those who opt in).
  Further candidates bump `N`; the clean `X.Y.Z` and its `vX.Y.Z` tag at GA
  are what close the phase and its milestone.
- Each phase has one matching GitHub milestone (titled
  `Phase N — <Name> (<major>.<minor>.x)`), not one per M-number; every PR
  merged while the phase is in flight — M-number work and chores alike — is
  associated with the phase's milestone on creation (retroactively if one
  slips through), so the milestone reads as the complete record of what the
  phase ships. The milestone is closed when the phase's final PR merges. The
  milestone's description lists each composing M-number with its own
  description as a bullet, so the phase-level summary and the per-milestone
  detail both stay visible in one place.

## Testing

- Generator output is snapshot-tested with the Roslyn testing SDK.
- Benchmarks compare against Dapper and raw ADO.NET (the `bench/` layout and
  CI policy come from the shared benchmarks overlay).

## Architecture

- The shared "one solution file (`.slnx`) per repo, at its root" rule extends
  here to "per repo or example": a self-contained example project gets its own
  `.slnx` at its own root.
