# Architecture

Rules governing physical project and solution structure (as opposed to logical design, which lives in `design-principles.md`):

- Repo/solution layout: source under `src/<ProjectName>/`, unit tests under `test/<ProjectName>.UnitTests/` (singular `test`), integration tests requiring a real dependency (e.g. a database provider) under `test/<ProjectName>.IntegrationTests/`, one solution file (`.slnx`) per repo or example, at its root, referencing every project beneath it.
- Centralize shared MSBuild properties (`TargetFramework`, `Nullable`, `TreatWarningsAsErrors`, etc.) in a `Directory.Build.props` at that same root instead of repeating them per `.csproj`.
- `dotnet pack` runs in CI from day one so packaging bugs surface early.
