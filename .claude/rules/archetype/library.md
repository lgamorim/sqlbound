# Archetype — Library (shipped, reusable package)

For code consumed by other projects (NuGet packages, shared libraries).
Compose with the five core rules.

Structure (deltas from `core/architecture.md`)
- Unit tests under `test/<ProjectName>.UnitTests/`; integration tests that need
  a real dependency under `test/<ProjectName>.IntegrationTests/`.

Packaging & compatibility
- `dotnet pack` runs in CI from day one so packaging bugs surface early.
- Target the full supported TFM matrix (e.g. `net8.0;net10.0`); CI runs the
  matrix on every OS you claim to support.
- The public API is a contract: follow semantic versioning, keep XML docs
  complete (they ship in the package), and treat any breaking change as a
  deliberate, documented major bump.

Testing
- Cover the public surface directly; internal helpers are tested through it,
  not around it.
