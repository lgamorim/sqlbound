# 8. Shipping 1.0: API stability, strong-naming, signing, and docs

## Status

Accepted

## Context

Phase 6 (Ship, M16) turns SqlBound from a set of prerelease packages into a stable `1.0.0`. A `1.0`
release is a public commitment: from it onward, semantic versioning governs what may change, and
several decisions become effectively irreversible once consumers depend on the packages. This ADR
records the ones made for the release so they are not silently revisited later.

The release is cut first as `1.0.0-rc.1` for review, then promoted to `1.0.0` unchanged; nothing in
this ADR is specific to the candidate versus the final.

## Decision

### Public-API stability, enforced mechanically

After `1.0.0`, the public surface of every shipping library follows semantic versioning: additive in
minor releases, breaking only in a major. This is enforced by
`Microsoft.CodeAnalysis.PublicApiAnalyzers`: each library carries `PublicAPI.Shipped.txt` and
`PublicAPI.Unshipped.txt`, and any change to a public type or member that is not reflected in those
files fails the build (`RS0016`/`RS0017`) under `TreatWarningsAsErrors`. Reviewing a public-surface
change therefore means reviewing a baseline diff — the commitment is tool-checked, not prose.

Until `1.0.0` actually ships to NuGet, the whole surface lives in `Unshipped.txt`; the GA promotion
moves it to `Shipped.txt`, after which removing a shipped entry is flagged as a breaking change.

The surface was deliberately narrowed before the freeze (for example, the migration decision helpers
`MigrationPlan`, `MigrationReverter`, and `MigrationStatusReport` became `internal`). Widening a
surface after `1.0` is a non-breaking minor change; narrowing it is not — so anything whose public
exposure was incidental was pulled back while it was still free to do so.

### Strong-naming

All assemblies are strong-named with a single committed key (`sqlbound.snk`). A strong-named
assembly cannot reference an unsigned one, and SqlBound's entire peer group — EF Core, Dapper,
Npgsql, the `Microsoft.Data.*` providers — is strong-named; shipping unsigned would permanently
exclude strong-named consumers. Adding or removing a strong name changes assembly identity and is a
binary-breaking change, so `1.0` is the only free moment to decide. The key is committed to the repo
because strong naming is an *identity* mechanism, not a security boundary — the key is not a secret.

### Package signing

Packages rely on **nuget.org repository signing**, which is applied automatically on publish and
gives consumers integrity and provenance tied to the account. **Author signing** is not done for
`1.0`: it requires a code-signing certificate whose private key must (per current CA rules) live on
hardware or a cloud HSM, which is real cost and CI complexity for a marginal trust gain on an
open-source library. Author signing is purely additive and can be enabled in any later release
without a breaking change; the release workflow leaves room for it.

### Documentation site

A DocFX site is published to GitHub Pages. It generates the API reference from the same XML doc
comments the build already enforces (`GenerateDocumentationFile` + `CS1591`) and renders the
existing `docs/*.md` conceptual pages, so the site reuses committed content rather than duplicating
it. This was chosen over both a hand-maintained site and shipping `1.0` with no site at all.

### Deferred publishing

The release pipeline exists but does not publish on its own: a `v*.*.*` tag builds and packs, and
publishing to nuget.org is a separate, opt-in manual action. See the release workflow for the gate.

## Consequences

**Positive**

- The `1.0` compatibility promise is enforced by the build, not by reviewer vigilance.
- Strong-named and unsigned consumers alike can reference SqlBound.
- Symbols, SourceLink, and deterministic CI builds let consumers step into exact committed sources.
- The documentation site stays in sync with the code because it is generated from it.

**Negative / trade-offs**

- Every intentional public-surface change now requires a baseline update in the same commit —
  small, ongoing friction that is the point of the mechanism.
- The committed strong-name key means anyone can build an assembly with the same identity; this is
  acceptable precisely because strong naming is not relied on as a security control.
- Without author signing, consumers who specifically require an author signature are not served at
  `1.0`; this is revisited only if there is real demand.

**Follow-up**

- The GA promotion moves the API baselines from unshipped to shipped and drops the prerelease suffix.
- Author signing (Azure Trusted Signing + the `sign` tool) can be added later if warranted.
