# 2. The source generator emits code from the method signature, not from `.sqlbound/` snapshots

## Status

Accepted

## Context

ADR 0001 confines database I/O to the `prepare` step and has the analyzer verify against committed
`.sqlbound/` snapshots. It deliberately leaves open which inputs the *source generator* uses when
emitting `DbDataReader` materialization code. Two coherent designs exist:

1. **Signature-driven codegen.** The generator emits code purely from the declared C# partial
   method signature (parameter types, return shape, nullability annotations). Snapshots feed only
   the analyzer; a missing or stale snapshot degrades verification but never breaks the build.
   Simpler generator, weaker guarantees, column access via `GetOrdinal` at runtime.
2. **Snapshot-driven codegen** (closer to SQLx). The generator reads snapshot metadata and can
   emit ordinal-based reads and database-informed conversions; a missing snapshot is a build
   error because codegen cannot proceed without it. Stronger guarantees and potentially faster
   generated code, but couples every build to snapshot freshness.

## Decision

**Signature-driven codegen.** The generator's only inputs are the `[SqlQuery]` attribute and the
declared C# partial method signature; it never reads `.sqlbound/` snapshots. Three reasons:

1. **The roadmap forces it.** Codegen (Phase 2) deliberately precedes verification (Phase 3)
   because the generator defines the shapes the verifier checks. When row materialization (M4) is
   built, no introspection, no `prepare` step, and no snapshot format exist yet — snapshot-driven
   codegen is unimplementable until M7–M9. Choosing it would invert the phase order.
2. **It matches ADR 0001's DX stance.** The load-bearing decision there was that stale or missing
   verification degrades gracefully instead of breaking the inner loop. Snapshot-driven codegen
   re-couples every build to snapshot freshness — the exact failure mode ADR 0001 exists to avoid.
3. **The runtime cost is modest and recoverable.** Generated code resolves column ordinals once
   via `GetOrdinal` before the read loop; per-row access is still straight-line typed getter
   calls — no reflection, no IL emit. If profiling (M6) shows ordinal resolution matters,
   snapshot-*informed* optimization can be layered on later without changing the public API (see
   Consequences).

## Consequences

**Positive**

- The build never depends on snapshot freshness; `.sqlbound/` remains purely a verification input,
  keeping one mental model: generator = code shape, analyzer = correctness against the database.
- The generator stays a fast, deterministic function of the compilation alone, with no
  `AdditionalFiles` coupling or cross-artifact cache invalidation concerns.
- Phase 2 can ship materialization end to end with zero verification infrastructure.

**Negative / trade-offs**

- A column/property mismatch (name or type) surfaces at **runtime** until Phase 3's analyzer
  catches it at build time against the snapshots. This is the accepted trade-off; it is the
  analyzer's job to close, not the generator's.
- Ordinal resolution happens once per query execution rather than being baked in at compile time.

**Possible future amendment (explicitly not a commitment)**

- If M6 benchmarks show `GetOrdinal` resolution to be a measurable cost, the generator may accept
  snapshot metadata as an *optional* optimization input — falling back to name-based resolution
  when absent, so a missing snapshot still never breaks the build. Any such change amends this
  ADR.

**Interaction with ADR 0001's open point**

- The analyzer's behavior for a query with *no* snapshot (M8's diagnostic design) is now
  unconstrained by codegen: since the build never needs snapshots, a missing one can be anything
  from silence to a warning, chosen purely on verification-DX grounds.
