# 2. Whether the source generator consumes `.sqlbound/` snapshots

## Status

Proposed — to be decided before M4 (row materialization) starts.

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

Not yet taken. To be resolved when M4 is planned, informed by generator prototyping.

## Consequences

Pending decision.
