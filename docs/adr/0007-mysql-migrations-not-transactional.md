# 7. MySQL migrations are not transactional

## Status

Accepted

## Context

M14 established that `migrate run` applies each migration in its own transaction, together with its
`_sqlbound_migrations` ledger row: if a script fails, the transaction rolls back and the migration
leaves neither a partial schema change nor a ledger row. This holds because SQL Server, PostgreSQL,
and SQLite all support **transactional DDL** — a `CREATE TABLE` can be rolled back.

M15 extends migrations to MySQL, which does not. In MySQL (and MariaDB), most DDL statements —
`CREATE TABLE`, `ALTER TABLE`, `DROP TABLE`, and so on — cause an **implicit commit**: the moment
one runs, any open transaction is committed, and the statement itself cannot be rolled back.
Wrapping a migration's DDL and its ledger insert in a `BEGIN … COMMIT` therefore buys no
atomicity — the DDL has already committed before the ledger row is written, and a failure between
the two cannot be undone. This is a property of the MySQL engine, not of the driver, and every
migration tool that supports MySQL documents the same limitation (Flyway, Liquibase, and others).

## Decision

On MySQL, migrations are applied **without a transaction**, and this is surfaced honestly rather
than papered over:

- `IMigrationLedger` exposes `SupportsTransactionalDdl`. `MySqlMigrationLedger` returns
  `false`; the SQL Server, PostgreSQL, and SQLite ledgers return `true`.
- `MigrationRunner` opens a per-migration transaction **only when the provider supports it**. For
  MySQL it runs the up-script and then writes the ledger row, both auto-committed. A pretend
  transaction is not used, because MySQL's implicit commit would make it silently meaningless.

The consequence is that a migration that fails partway on MySQL can leave the database in a partial
state, and — if the failure lands between the DDL and the ledger write — an applied change that the
ledger does not record. `migrate run` still stops at the first failure and reports which migration
failed, so the operator can inspect and fix it by hand.

## Consequences

**Positive**

- MySQL is supported honestly: the engine never claims an atomicity guarantee the database cannot
  provide.
- The capability lives on the ledger, the per-provider object the runner already holds, so the
  engine stays provider-neutral — it asks whether to use a transaction rather than sniffing the
  provider.
- SQL Server, PostgreSQL, and SQLite keep full per-migration atomicity, unchanged from M14.

**Negative / trade-offs**

- A failed migration on MySQL is not automatically rolled back; recovery may require manual
  cleanup. This matches every other MySQL migration tool but is a real difference from the other
  three providers.
- Authors targeting MySQL should keep migrations small and, where possible, idempotent, so a
  partial failure is easy to reconcile.

**Follow-up**

- None planned. This is an inherent MySQL limitation, documented in `docs/migrations.md` alongside
  the per-provider matrix rather than worked around.
