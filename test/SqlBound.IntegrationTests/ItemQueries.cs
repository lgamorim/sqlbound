using System.Data.Common;

namespace SqlBound.IntegrationTests;

public static partial class ItemQueries
{
    [SqlQuery("SELECT id AS Id, name AS Name, price AS Price FROM items WHERE category = @category ORDER BY id")]
    public static partial Task<IReadOnlyList<Item>> GetByCategoryAsync(
        DbConnection connection, string category, CancellationToken cancellationToken = default);

    [SqlQuery("SELECT id AS Id, name AS Name, price AS Price FROM items WHERE id = @id")]
    public static partial Task<Item> GetByIdAsync(
        DbConnection connection, int id, CancellationToken cancellationToken = default);

    [SqlQuery("SELECT id AS Id, name AS Name, price AS Price FROM items WHERE id = @id")]
    public static partial Task<Item?> FindByIdAsync(
        DbConnection connection, int id, CancellationToken cancellationToken = default);

    [SqlQuery("SELECT COUNT(*) FROM items WHERE category = @category")]
    public static partial Task<int> CountByCategoryAsync(
        DbConnection connection, string category, CancellationToken cancellationToken = default);

    [SqlQuery("SELECT name FROM items WHERE category = @category ORDER BY id")]
    public static partial Task<IReadOnlyList<string>> GetNamesByCategoryAsync(
        DbConnection connection, string category, CancellationToken cancellationToken = default);

    [SqlQuery("SELECT id AS Id, name AS Name, price AS Price FROM items WHERE category = @category ORDER BY id")]
    public static partial IAsyncEnumerable<Item> StreamByCategoryAsync(
        DbConnection connection, string category, CancellationToken cancellationToken = default);

    [SqlExecute("DELETE FROM items WHERE category = @category")]
    public static partial Task<int> DeleteByCategoryAsync(
        DbConnection connection, string category, CancellationToken cancellationToken = default);
}

public sealed record Item(int Id, string Name, decimal? Price);
