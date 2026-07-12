# Migrations

SqlBound applies schema changes as ordered SQL-file migrations, tracked in a database-side ledger.
This document covers the on-disk format, the ledger, and the CLI commands. The format decisions are
recorded in [ADR 0006](adr/0006-migration-file-format.md).

> **Status.** M13 ships the migration model, the SQL Server ledger, `migrate add`, and
> `database create`/`database drop`. Applying and reverting migrations (`migrate run`, `migrate
> revert`, `migrate status`) arrives in M14, and the other providers in M15.

## File format

A migration is a pair of plain-SQL files in the migrations directory (default `./migrations`):

```
migrations/
  20260712143000_create_items.up.sql     -- forward script (required)
  20260712143000_create_items.down.sql   -- rollback script (optional)
```

- **Version** — the leading `yyyyMMddHHmmss` UTC timestamp. Timestamps sort chronologically and,
  unlike sequential numbers, do not collide when two branches each add a migration.
- **Name** — a `snake_case` slug describing the change.
- **Direction** — `.up.sql` is applied going forward; `.down.sql` reverses it.

The down script is optional *per migration*. A migration with only an up file is **irreversible**:
`migrate revert` will refuse to roll back past it rather than silently doing nothing. Author
one-way changes (data backfills, drops) this way with `migrate add --irreversible`.

Each file is ordinary SQL — no magic comments, no in-file section markers — so you can run one
directly in any client. The directory is validated as a set when it is loaded: a malformed file
name, a duplicated version, an up/down pair whose names disagree, or a rollback with no matching
forward script is rejected.

## The ledger

Applied migrations are recorded in a table named `_sqlbound_migrations`, created on demand:

| Column           | Meaning                                      |
| ---------------- | -------------------------------------------- |
| `version`        | The migration's timestamp version (primary key) |
| `name`           | The migration's name slug                    |
| `checksum`       | SHA-256 of the applied up-script             |
| `applied_on_utc` | When the migration was applied               |
| `execution_ms`   | How long the up-script took                  |

The checksum covers the up-script only, with line endings normalized to `\n` first (so a CRLF
checkout does not invalidate it). It exists to detect edits to a migration that has already been
applied — the classic migration hazard — which a later `migrate run` will flag rather than silently
skip.

## Commands

### `migrate add`

Scaffolds a new migration file pair with the current UTC time as the version:

```bash
dotnet sqlbound migrate add "create items"
# created ./migrations/20260712143000_create_items.up.sql
# created ./migrations/20260712143000_create_items.down.sql

dotnet sqlbound migrate add "backfill emails" --irreversible
# created ./migrations/20260712150000_backfill_emails.up.sql
```

- The name is slugged to `snake_case`; `"create items"` becomes `create_items`.
- `--migrations <dir>` sets the directory (default `./migrations`); it is created if missing.
- `--irreversible` writes only the up script.

### `database create` / `database drop`

Create or drop the database named by the connection string's `Initial Catalog`, connecting to
`master` to do so:

```bash
export SQLBOUND_DATABASE_URL="sqlserver://sa:password@localhost:1433/myapp?TrustServerCertificate=true"
dotnet sqlbound database create   # database 'myapp' is ready.
dotnet sqlbound database drop      # database 'myapp' is dropped.
```

- `--connection` overrides `SQLBOUND_DATABASE_URL`.
- Both are idempotent: `create` does nothing if the database exists, `drop` does nothing if it does
  not. `drop` forces out open connections before dropping.
- The target name is bracketed with `QUOTENAME` server-side, so it can never be interpreted as SQL.
- Both refuse a connection string that names no database or names a system database
  (`master`, `model`, `msdb`, `tempdb`).
- **SQL Server only in this release.** The other providers follow in M15.
