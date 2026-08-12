# Overlay — Benchmarks (BenchmarkDotNet)

Add when the project tracks performance.

- Benchmark projects live under `bench/<ProjectName>/` and join the solution so
  CI *builds* them.
- CI never *runs* benchmarks — numbers from shared runners are noise. Produce
  baselines locally and record them in `docs/`.
- Smoke-test a benchmark's plumbing with `--job Dry`
  (e.g. `dotnet run -c Release --project bench/<ProjectName> -- --job Dry`).
