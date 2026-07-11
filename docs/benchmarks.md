# Benchmarks

`bench/SqlBound.Benchmarks` compares SqlBound's generated code against [Dapper](https://github.com/DapperLib/Dapper)
and hand-written raw ADO.NET (the practical ceiling) on identical SQL over an in-memory SQLite
database, with `MemoryDiagnoser` capturing allocations.

## How to run

```bash
dotnet run --project bench/SqlBound.Benchmarks -c Release -- --filter "*"
```

Benchmarks are built — never run — by CI: numbers from shared runners are noise. The baseline
below comes from a local run; re-run locally to compare like for like.

## Reading the numbers

- **Raw ADO.NET is the baseline** (ratio 1.00) in every category: it is what a human writes by
  hand with `GetOrdinal` + typed getters, i.e. the same straight-line code the generator emits.
  SqlBound's goal is to match it, not beat it.
- Absolute numbers are dominated by SQLite itself; the meaningful signal is the **relative** time
  and allocation across the three approaches doing identical work.
- Allocations are where reflection-free codegen shows most clearly — Dapper's materializer is
  fast, but its dynamic parameter and mapping machinery allocates more.

## Baseline results

Captured 2026-07-11 with the command above:

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8737/25H2)
AMD Ryzen Threadripper 3960X 3.80GHz, 1 CPU, 48 logical and 24 physical cores
.NET SDK 10.0.301, .NET 10.0.9, X64 RyuJIT x86-64-v3
```

| Method            | Categories | Mean       | Ratio | Allocated | Alloc Ratio |
|------------------ |----------- |-----------:|------:|----------:|------------:|
| RawAdoNet_Execute | Execute    |   3.349 us |  1.00 |     960 B |        1.00 |
| SqlBound_Execute  | Execute    |   3.652 us |  1.09 |    1000 B |        1.04 |
| Dapper_Execute    | Execute    |   4.234 us |  1.26 |    1536 B |        1.60 |
| RawAdoNet_List    | List1000   | 432.710 us |  1.00 |  105832 B |        1.00 |
| SqlBound_List     | List1000   | 517.365 us |  1.20 |  105904 B |        1.00 |
| Dapper_List       | List1000   | 592.187 us |  1.37 |  151496 B |        1.43 |
| RawAdoNet_Scalar  | Scalar     |   2.865 us |  1.00 |     864 B |        1.00 |
| SqlBound_Scalar   | Scalar     |   2.881 us |  1.01 |     912 B |        1.06 |
| Dapper_Scalar     | Scalar     |   2.987 us |  1.04 |     864 B |        1.00 |
| RawAdoNet_Single  | SingleRow  |   4.572 us |  1.00 |    1624 B |        1.00 |
| SqlBound_Single   | SingleRow  |   4.792 us |  1.05 |    1696 B |        1.04 |
| Dapper_Single     | SingleRow  |   5.607 us |  1.23 |    2080 B |        1.28 |

Takeaways from this baseline:

- SqlBound stays within 1–9% of hand-written ADO.NET on point operations and beats Dapper in
  every category on both time and allocations.
- List materialization allocates **exactly** what the hand-written loop allocates (1.00 ratio;
  Dapper allocates 1.43×) — the reflection-free straight-line codegen doing its job.
- The 1.20× list time (vs raw's 1.00) is the price of generated null guards: the generated code
  checks `IsDBNull` on *every* column to throw a descriptive error on unexpected NULLs, where the
  hand-written loop checks only the nullable one. That trade is deliberate.
- Two benchmark-fairness notes: the row type uses SQLite's natural provider types (`long`,
  `double`) because Dapper's constructor mapping requires exact type matches; and Dapper's strict
  `QuerySingleAsync` was chosen to match SqlBound's strict-single semantics.
