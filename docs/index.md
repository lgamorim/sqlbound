# Documentation

Guides and design records for SqlBound. For the generated type-level reference, see the
[API Reference](xref:SqlBound).

## Guides

- [Compile-time verification](verification.md) — the `prepare` step, offline snapshots, and the
  `SQLB###` diagnostics.
- [Diagnostics](diagnostics.md) — the full `SQLB###` catalog.
- [Introspection](introspection.md) — what each provider can and cannot describe.
- [Migrations](migrations.md) — the file format, the ledger, and per-provider behaviour.
- [Using SqlBound alongside Dapper](dapper-coexistence.md) — sharing a connection and transaction.
- [Benchmarks](benchmarks.md) — methodology and baseline numbers versus Dapper and raw ADO.NET.

## Architecture decisions

The [ADRs](adr/0001-verification-split.md) record the load-bearing design choices, from the
verification split through the 1.0 shipping decisions.
