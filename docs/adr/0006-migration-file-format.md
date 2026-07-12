# 6. Migrations are paired up/down SQL files with a timestamp version and a checksummed ledger

## Status

Accepted

## Context

Phase 5 (Migrations & CLI) adds `migrate add/run/revert` and a migration history to SqlBound.
M13 defines the shapes the later milestones apply against: how a migration is stored on disk, how
migrations are ordered, and how the database records which ones ran. Three questions had to be
settled before writing any code.

**1. On-disk format.** Three candidates:

- *Paired files* — `{version}_{name}.up.sql` and `{version}_{name}.down.sql`.
- *Single file with markers* — one `.sql` file split into up/down sections by magic comments
  (`-- +migrate up` / `-- +migrate down`).
- *Up-only* — a single `.sql` file per migration, no rollback.

`migrate revert` is a committed command (see `CLAUDE.md`), which rules out up-only outright. Between
the remaining two, single-file-with-markers makes a comment semantically load-bearing: the file is
no longer plain SQL a DBA can paste into a client unchanged, and the tool must own a parser whose
bugs corrupt migrations. It also muddies the checksum (see below) — the up and down halves live in
one file, so any edit to the rollback text changes the hash of the thing that was applied. Paired
files keep every file pure, runnable SQL and give the checksum an unambiguous target. This is also
the layout SQLx uses for its reversible migrations, which is SqlBound's lineage.

**2. Version scheme.** Sequential (`0001`, `0002`, …) reads nicely but collides the moment two
branches each add "the next" migration — a routine occurrence on a team, and one that produces two
different migrations claiming the same number after a merge. A UTC timestamp (`yyyyMMddHHmmss`)
sorts chronologically under a plain lexicographic/numeric ordering, is monotonic, and effectively
never collides across branches. This is why Rails, SQLx (default), and Flyway's timestamp mode all
use it. The cost — a wall-clock read makes `migrate add` non-deterministic — is neutralized by
injecting `TimeProvider`, which `testing.md` already mandates for exactly this reason.

**3. History tracking.** The database needs a durable record of which migrations have been applied,
readable before a `run` to compute the pending set and before a `revert` to find the top of the
stack. This is a ledger table the tool owns.

## Decision

**File format.** A migration is a pair of files in the migrations directory:

- `{version}_{name}.up.sql` — the forward script. Required.
- `{version}_{name}.down.sql` — the rollback script. **Optional per migration.**

`version` is a 14-digit UTC timestamp `yyyyMMddHHmmss`. `name` is a slug of `[A-Za-z0-9_]`
(other characters collapse to `_`). If the `.down.sql` file is absent the migration is
irreversible: `migrate revert` past it fails with an actionable message rather than silently doing
nothing. `migrate add` scaffolds both files by default; `--irreversible` skips the down file.

A `.down.sql` with no matching `.up.sql` is an error (an orphaned rollback), as are two migrations
sharing a version. There is no "no gaps" rule — timestamp versions are deliberately non-contiguous.

**Checksum.** Each migration carries a SHA-256 hash (lowercase hex) of its **up-script only**, with
line endings normalized to `\n` before hashing so a CRLF checkout does not invalidate it (the same
normalization `SnapshotStore` already applies to snapshots). The checksum detects after-the-fact
edits to an already-applied migration — the classic migration footgun — and is what the ledger
stores. The down-script is excluded so a rollback can be corrected without falsely flagging the
forward migration as tampered.

**Ledger.** A table named `_sqlbound_migrations`, created on demand and idempotently by the
provider's ledger implementation, with the provider-neutral shape:

| Column           | Meaning                                        |
| ---------------- | ---------------------------------------------- |
| `version`        | The migration's timestamp version (PK)         |
| `name`           | The migration's name slug                      |
| `checksum`       | SHA-256 of the applied up-script               |
| `applied_on_utc` | When the migration was applied                 |
| `execution_ms`   | How long the up-script took                    |

The abstraction is `SqlBound.Migrations.IMigrationLedger`; M13 ships the SQL Server implementation
in `SqlBound.SqlServer`. Other providers follow in M15 — the abstraction is introduced now because,
unlike a speculative interface, a second implementation is already scheduled, and the ledger's SQL
genuinely differs per provider. M13 defines only the read side (`EnsureCreatedAsync`,
`GetAppliedAsync`); the write side arrives in M14 with `migrate run`, motivated by a failing test
rather than added speculatively.

## Consequences

**Positive**

- Every migration file is plain, runnable SQL — no parser, no magic comments.
- Timestamp versions make concurrent authoring on separate branches collision-free.
- The checksum has one unambiguous target (the up-script) and survives CRLF round-trips.
- Reversibility is a per-migration property, so trivial one-way migrations (data backfills) are not
  forced to carry an empty rollback file.

**Negative / trade-offs**

- Two files per reversible migration instead of one.
- Timestamp versions are less human-memorable than `0001`/`0002` and cannot be read as a count.
- Mixing reversible and irreversible migrations means `migrate revert` can only roll back a
  contiguous reversible suffix; the failure when it hits an irreversible migration must be clear.

**Follow-up**

- M14 adds the ledger write side and the `migrate run`/`revert`/`status` engine.
- M15 extends `IMigrationLedger` and the database-lifecycle commands to SQLite, Postgres, and MySQL,
  where the transactional-DDL differences (notably MySQL's implicit commit on DDL) must be
  reconciled.
