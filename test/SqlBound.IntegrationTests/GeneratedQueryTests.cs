using Dapper;
using Microsoft.Data.Sqlite;

namespace SqlBound.IntegrationTests;

public class GeneratedQueryTests
{
    [Fact]
    public async Task Should_MaterializeRowsIncludingNulls_When_GeneratedQueryExecutes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var session = new SqlSession(connection);
        await session.RunAsync(
            "CREATE TABLE items (id INTEGER PRIMARY KEY, name TEXT NOT NULL, price REAL NULL, category TEXT NOT NULL)",
            cancellationToken: TestContext.Current.CancellationToken);
        await session.RunAsync(
            "INSERT INTO items (id, name, price, category) VALUES (1, 'hammer', 9.5, 'tools'), (2, 'nails', NULL, 'tools'), (3, 'apple', 0.5, 'food')",
            cancellationToken: TestContext.Current.CancellationToken);

        var items = await ItemQueries.GetByCategoryAsync(
            connection, "tools", TestContext.Current.CancellationToken);

        Assert.Equal(
            [new Item(1, "hammer", 9.5m), new Item(2, "nails", null)],
            items);
    }

    [Fact]
    public async Task Should_ReadDapperWrites_When_GeneratedQueryRunsOnSharedConnection()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await connection.ExecuteAsync(
            "CREATE TABLE items (id INTEGER PRIMARY KEY, name TEXT NOT NULL, price REAL NULL, category TEXT NOT NULL)");
        await connection.ExecuteAsync(
            "INSERT INTO items (id, name, price, category) VALUES (@Id, @Name, @Price, @Category)",
            new { Id = 1, Name = "from-dapper", Price = 1.25, Category = "shared" });

        var items = await ItemQueries.GetByCategoryAsync(
            connection, "shared", TestContext.Current.CancellationToken);

        var item = Assert.Single(items);
        Assert.Equal(new Item(1, "from-dapper", 1.25m), item);
    }
}
