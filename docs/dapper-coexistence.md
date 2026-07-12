# Using SqlBound alongside Dapper

SqlBound and [Dapper](https://github.com/DapperLib/Dapper) are designed to run in the same project,
on the same `DbConnection` and `DbTransaction`, with no conflict. They solve different halves of the
data-access problem:

- **SqlBound** handles SQL that is *static and known at build time*. It generates straight-line
  ADO.NET reader code ahead of time and can verify the query against your schema before it runs.
- **Dapper** handles SQL that is *composed at runtime* — dynamic filters, search builders, `IN`
  clauses of varying arity — mapping results with reflection.

Reach for whichever fits the query in front of you. Nothing forces an either/or choice.

## Why they don't collide

Coexistence is a deliberate design constraint, not a happy accident. Concretely:

1. **SqlBound never owns the connection.** Every entry point takes an already-open `DbConnection`
   (and optionally a `DbTransaction`) that the caller controls. SqlBound never opens, closes, pools,
   or wraps it — so Dapper can use the very same connection object.
2. **No method-name clash.** Dapper's surface is a set of `Query*` / `Execute*` / `ExecuteScalar*`
   extension methods on `IDbConnection`. SqlBound deliberately defines **none** of those. Its
   primary API is generated `static partial` methods you name yourself; its dynamic escape hatch,
   `SqlSession`, uses distinct verbs (`RunAsync`, `FetchScalarAsync`). Importing both
   `using SqlBound;` and `using Dapper;` in one file never produces an ambiguous-call error.
3. **No global configuration state.** SqlBound resolves type conversions at compile time in the
   source generator; it has no equivalent of Dapper's static `SqlMapper` registry, so there is no
   shared mutable state for the two libraries to fight over.

## Sharing a connection

Both libraries take the same open `DbConnection`. Use the generated method for the fixed query and
Dapper for the dynamic one:

```csharp
using System.Data.Common;
using Dapper;
using SqlBound;

public static partial class Catalog
{
    // Static, build-time-known query -> SqlBound generates the body and can verify it.
    [SqlQuery("SELECT id AS Id, name AS Name, price AS Price FROM items WHERE category = @category ORDER BY id")]
    public static partial Task<IReadOnlyList<Item>> GetByCategoryAsync(
        DbConnection connection, string category, CancellationToken cancellationToken = default);
}

public sealed record Item(int Id, string Name, decimal? Price);

// ...

await connection.OpenAsync();

// SqlBound for the fixed query:
var byCategory = await Catalog.GetByCategoryAsync(connection, "books");

// Dapper on the same connection for a query composed at runtime:
var sql = $"SELECT * FROM items WHERE {BuildDynamicFilter(request)} ORDER BY id";
var filtered = await connection.QueryAsync<Item>(sql, request.Parameters);
```

## Sharing a transaction

Enlist both libraries in one `DbTransaction` — construct a `SqlSession` with it, and pass the same
transaction to Dapper's calls. Either library's writes participate in the same atomic unit:

```csharp
await using var transaction = await connection.BeginTransactionAsync();

// SqlBound's dynamic surface, enlisted in the transaction:
var session = new SqlSession(connection, transaction);
await session.RunAsync(
    "UPDATE accounts SET balance = balance - @amount WHERE id = @id",
    new SqlParameters(("@amount", 100m), ("@id", fromId)));

// Dapper, same transaction:
await connection.ExecuteAsync(
    "UPDATE accounts SET balance = balance + @amount WHERE id = @id",
    new { amount = 100m, id = toId },
    transaction);

await transaction.CommitAsync();
```

Generated `[SqlQuery]` / `[SqlExecute]` methods that accept a `DbTransaction` parameter enlist the
same way.

## What each library is best at

| Situation | Reach for |
| --- | --- |
| Query text fixed at build time | SqlBound `[SqlQuery]` / `[SqlExecute]` (verified, reflection-free) |
| Want compile-time schema checking | SqlBound (`sqlbound prepare` + analyzer) |
| Native AOT / trimming | SqlBound (no reflection, no IL emit) |
| SQL composed at runtime, mapped to types | Dapper `Query*` |
| One-off dynamic non-query or scalar, no generator | SqlBound `SqlSession.RunAsync` / `FetchScalarAsync`, or Dapper |

SqlBound's `SqlSession` intentionally covers only non-query execution (`RunAsync`) and scalar reads
(`FetchScalarAsync<T>`). Dynamic queries that materialize *rows* into types are Dapper's strength,
and SqlBound does not try to duplicate it.

## Guaranteed by CI

Coexistence is not just documented — it is a permanent regression test. The
`test/SqlBound.IntegrationTests` suite exercises SqlBound and Dapper against the same connection and
transaction on every CI run, so a future change that reintroduces a naming or lifecycle conflict
fails the build.
