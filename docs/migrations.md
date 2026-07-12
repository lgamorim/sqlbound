# Migrations

SqlBound applies schema changes as ordered SQL-file migrations, tracked in a database-side ledger.
This document covers the on-disk format, the ledger, and the CLI commands. The format decisions are
recorded in [ADR 0006](adr/0006-migration-file-format.md); the MySQL transactional caveat in
[ADR 0007](adr/0007-mysql-migrations-not-transactional.md).

The full command set — `migrate add`, `migrate run`, `migrate revert`, `migrate status`, and
`database create`/`database drop` — works against **SQL Server, SQLite, PostgreSQL, and MySQL**.
The provider is chosen from the connection URL's scheme (see [verification.md](verification.md)).

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

### `migrate run`

Applies every pending migration, in version order:

```bash
export SQLBOUND_DATABASE_URL="sqlserver://sa:password@localhost:1433/myapp?TrustServerCertificate=true"
dotnet sqlbound migrate run
# applied 20260712143000_create_items (18 ms)
# applied 1 migration(s).
```

- On SQL Server, PostgreSQL, and SQLite, each migration's up-script and its ledger row commit in
  **one transaction**: if a script fails, that migration is rolled back and the run stops, while
  every earlier migration stays applied. On **MySQL** there is no such rollback — see the matrix
  below and [ADR 0007](adr/0007-mysql-migrations-not-transactional.md).
- `run` refuses to proceed on two inconsistencies: an already-applied migration whose up-script has
  been **edited** (checksum drift), and a **pending migration ordered before** one already applied
  (a late-merged branch). Fix the directory rather than the database.
- `--migrations <dir>` and `--connection` work as elsewhere.

> **Batch separators.** Each script runs as a single command; SQL Server's `GO` separator is **not**
> supported, so a migration needing multiple batches (e.g. `CREATE PROCEDURE` followed by more SQL)
> must be split into separate migrations. This may be revisited in a later release.

#### Per-provider behaviour

| Provider | Migrations transactional? | `database create` / `drop` |
| --- | --- | --- |
| SQL Server | Yes | Connects to `master`; `QUOTENAME`-quoted; force-drops open connections |
| PostgreSQL | Yes | Connects to `postgres`; drops with `WITH (FORCE)` |
| SQLite | Yes | The `Data Source` file *is* the database: create materializes it, drop deletes it |
| MySQL | **No** — DDL auto-commits, so a failed migration is not rolled back ([ADR 0007](adr/0007-mysql-migrations-not-transactional.md)) | `CREATE`/`DROP DATABASE IF (NOT) EXISTS` from a no-default-database connection |

### `migrate revert`

Rolls back the most recently applied migration by running its down-script:

```bash
dotnet sqlbound migrate revert
# reverted 20260712143000_create_items.
```

- The down-script and the ledger removal commit in one transaction.
- `revert` refuses if the target migration is **irreversible** (no down script) or its files are
  **missing**; it is a no-op ("nothing to revert") when the ledger is empty.
- Reverts one migration per invocation.

### `migrate status`

Reports each migration's state without changing anything:

```bash
dotnet sqlbound migrate status
# 20260712143000_create_items  applied  2026-07-12 14:30:07Z
# 20260712150000_backfill_emails  pending
```

- States: **applied**, **pending**, **drifted** (up-script edited since it was applied), and
  **missing** (in the ledger, but the file is gone).

### `database create` / `database drop`

Create or drop the database named by the connection string, connecting to the provider's
maintenance database (or, for SQLite, acting on the file) to do so:

```bash
export SQLBOUND_DATABASE_URL="sqlserver://sa:password@localhost:1433/myapp?TrustServerCertificate=true"
dotnet sqlbound database create   # database 'myapp' is ready.
dotnet sqlbound database drop      # database 'myapp' is dropped.
```

- `--connection` overrides `SQLBOUND_DATABASE_URL`.
- Both are idempotent: `create` does nothing if the database exists, `drop` does nothing if it does
  not. The server providers force out open connections before dropping.
- Identifiers are quoted for the provider, so a database name can never be interpreted as SQL.
- All providers refuse a connection string that names no database or names a system database. The
  per-provider mechanics are in the matrix above.
- `create`/`drop` are administrative operations: the connection must have the privilege to create
  databases (e.g. a privileged/`root` user on the server providers).
