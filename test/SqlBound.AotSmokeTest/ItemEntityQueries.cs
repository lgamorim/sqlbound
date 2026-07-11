using System.Data.Common;

namespace SqlBound.AotSmokeTest;

internal static partial class ItemEntityQueries
{
    [SqlQuery("SELECT id AS Id, name AS Name, price AS Price FROM items WHERE category = @category ORDER BY id")]
    internal static partial Task<IReadOnlyList<ItemEntity>> GetByCategoryAsync(
        DbConnection connection, string category, CancellationToken cancellationToken = default);
}

internal sealed class ItemEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal? Price { get; set; }
}
