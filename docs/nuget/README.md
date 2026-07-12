<p align="center">
  <img src="https://raw.githubusercontent.com/lgamorim/sqlbound/master/assets/logo.png" alt="SqlBound" width="280">
</p>

# SqlBound

**Compile-time verified SQL for .NET.** SqlBound checks your SQL against a real database schema at
build time and materializes rows with a Roslyn source generator — no runtime reflection, no IL emit,
Native AOT- and trimming-compatible.

It is designed to **coexist with Dapper** on the same `DbConnection` and `DbTransaction`: SqlBound
targets static, build-time-known queries; Dapper stays the tool for runtime-composed SQL. SqlBound
never owns or wraps connections and defines no `Query*`/`Execute*` extension methods, so the two
libraries share a project without conflict.

## Example

```csharp
using SqlBound;

public static partial class ItemQueries
{
    [SqlQuery("SELECT Id FROM dbo.Items")]
    public static partial Task<IReadOnlyList<int>> GetIdsAsync(DbConnection connection);
}
```

The generator emits straight-line `DbDataReader` code. The `sqlbound prepare` CLI step verifies each
query against your schema and commits an offline JSON snapshot, so the in-IDE analyzer validates
without a live database connection.

## Packages

| Package | Purpose |
| --- | --- |
| `SqlBound` | Runtime core (dependency-free). |
| `SqlBound.Generators` | Source generator + analyzer (build-time only). |
| `SqlBound.Introspection` | Provider-neutral introspection contracts. |
| `SqlBound.Migrations` | SQL-file migration model and ledger. |
| `SqlBound.SqlServer` · `.Sqlite` · `.Npgsql` · `.MySql` | Per-provider introspection and type mapping. |
| `sqlbound` (dotnet tool) | CLI: `prepare`, `verify`, `migrate`, `database`. |

## Providers

SQL Server, SQLite, PostgreSQL, and MySQL — query verification, reflection-free materialization, and
SQL-file migrations, each reflecting what the underlying engine actually supports.

## Documentation

Guides, provider matrix, and architecture decision records:
https://github.com/lgamorim/sqlbound

## License

Apache-2.0
