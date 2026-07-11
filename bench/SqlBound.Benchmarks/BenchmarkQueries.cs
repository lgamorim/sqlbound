using System.Data.Common;

namespace SqlBound.Benchmarks;

public static partial class BenchmarkQueries
{
    [SqlQuery("SELECT id AS Id, name AS Name, price AS Price FROM items")]
    public static partial Task<IReadOnlyList<Item>> GetAllAsync(
        DbConnection connection, CancellationToken cancellationToken = default);

    [SqlQuery("SELECT id AS Id, name AS Name, price AS Price FROM items WHERE id = @id")]
    public static partial Task<Item> GetByIdAsync(
        DbConnection connection, int id, CancellationToken cancellationToken = default);

    [SqlQuery("SELECT COUNT(*) FROM items")]
    public static partial Task<int> CountAsync(
        DbConnection connection, CancellationToken cancellationToken = default);

    [SqlExecute("UPDATE items SET price = @price WHERE id = @id")]
    public static partial Task<int> RepriceAsync(
        DbConnection connection, int id, decimal price, CancellationToken cancellationToken = default);
}

// Uses SQLite's natural provider types (INTEGER => long, REAL => double) so all three contenders
// do identical work: Dapper's constructor mapping requires exact type matches and cannot narrow.
public sealed record Item(long Id, string Name, double? Price);
