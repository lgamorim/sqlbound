using System.Data.Common;

namespace SqlBound.IntegrationTests;

public static partial class ItemEntityQueries
{
    [SqlQuery("SELECT id AS Id, name AS Name, price AS Price FROM items WHERE category = @category ORDER BY id")]
    public static partial Task<IReadOnlyList<ItemEntity>> GetByCategoryAsync(
        DbConnection connection, string category, CancellationToken cancellationToken = default);
}

public sealed class ItemEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal? Price { get; set; }
}
