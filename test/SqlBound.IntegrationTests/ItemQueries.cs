using System.Data.Common;

namespace SqlBound.IntegrationTests;

public static partial class ItemQueries
{
    [SqlQuery("SELECT id AS Id, name AS Name, price AS Price FROM items WHERE category = @category ORDER BY id")]
    public static partial Task<IReadOnlyList<Item>> GetByCategoryAsync(
        DbConnection connection, string category, CancellationToken cancellationToken = default);
}

public sealed record Item(int Id, string Name, decimal? Price);
