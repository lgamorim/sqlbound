# Verification workflow

SqlBound's compile-time verification (the SQLx `query!` equivalent) is a two-stage pipeline
(ADR 0001): an explicit, I/O-bound **prepare** step describes every query against a real
database and commits the result as snapshots; the fast, offline **analyzer** compares each
`[SqlQuery]`/`[SqlExecute]` method against those snapshots on every build and in the IDE.
Verification is opt-in by snapshot presence (ADR 0003): projects that never run prepare are
never nagged.

## Preparing snapshots

```bash
export SQLBOUND_DATABASE_URL="sqlserver://sa:password@localhost:1433/mydb?TrustServerCertificate=true"
dotnet sqlbound prepare --project src/MyApp
```

- `SQLBOUND_DATABASE_URL` accepts a `sqlserver://user:pass@host:port/database?Option=value` URL,
  a `sqlite://<path>` URL, or a raw ADO.NET connection string (treated as SQL Server, for backward
  compatibility with the single-provider convention M7 introduced); `--connection` overrides the
  environment variable. See [introspection.md](introspection.md) for what each provider can and
  cannot describe.
- Prepare walks the project's C# sources (no build required), describes each distinct command
  text, and reconciles `.sqlbound/`: one `query-<sha256>.json` per query, keyed by the hash of
  the raw command text, with orphaned files pruned. **Commit the `.sqlbound/` directory.**
- Command texts must be inline string literals (regular, verbatim, raw, or concatenations of
  literals); SQL referenced through a `const` is reported as a warning and skipped.
- A run with describe failures (exit 1) writes nothing, so a broken query can never prune or
  overwrite the last good snapshots.

## Consuming snapshots

`PackageReference` consumers of `SqlBound.Generators` get the wiring automatically — the package
ships a props file adding `.sqlbound/*.json` to `AdditionalFiles`. The analyzer then reports
SQLB101–111 (see [diagnostics.md](diagnostics.md)): a query without a snapshot, a stale or
unreadable snapshot, and column/parameter mismatches against the described metadata.

After changing a query's SQL or the database schema, re-run `prepare`; the IDE flags the
affected methods (SQLB101/SQLB102) until the committed snapshots catch up.

## Keeping CI honest

```yaml
- name: Verify snapshots are current
  run: dotnet sqlbound prepare --project src/MyApp --check
  env:
    SQLBOUND_DATABASE_URL: ${{ secrets.SQLBOUND_DATABASE_URL }}
```

`prepare --check` regenerates the snapshots in memory and compares: exit 0 when everything
matches, exit 2 listing each missing, stale, or orphaned file — without touching the working
tree. This is the drift gate ADR 0001 required; builds themselves stay database-free.

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | Success (or `--check` found no drift) |
| 1 | Discovery, connection, or describe failure — nothing written |
| 2 | `--check` found stale snapshots |
