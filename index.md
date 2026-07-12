# SqlBound

**Compile-time verified SQL for .NET.** SqlBound checks your SQL against a real database schema at
build time and materializes rows with a Roslyn source generator — no runtime reflection, no IL emit,
Native AOT- and trimming-compatible. It coexists cleanly with [Dapper](https://github.com/DapperLib/Dapper)
on the same connection and transaction.

## Get started

- **[Documentation](docs/index.md)** — verification, migrations, providers, and the Dapper coexistence guide.
- **[API Reference](xref:SqlBound)** — the full public surface of every package.
- **[Source on GitHub](https://github.com/lgamorim/sqlbound)** — issues, releases, and the changelog.

```csharp
using SqlBound;

public static partial class ItemQueries
{
    [SqlQuery("SELECT Id FROM dbo.Items")]
    public static partial Task<IReadOnlyList<int>> GetIdsAsync(DbConnection connection);
}
```

The generator emits the method body. `sqlbound prepare` verifies the query against your schema and
commits an offline snapshot, so the in-IDE analyzer validates without a live database connection.
