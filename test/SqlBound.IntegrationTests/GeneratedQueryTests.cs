using Dapper;
using Microsoft.Data.Sqlite;

namespace SqlBound.IntegrationTests;

public class GeneratedQueryTests
{
    private static async Task<SqliteConnection> OpenSeededConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var session = new SqlSession(connection);
        await session.RunAsync(
            "CREATE TABLE items (id INTEGER PRIMARY KEY, name TEXT NOT NULL, price REAL NULL, category TEXT NOT NULL)",
            cancellationToken: TestContext.Current.CancellationToken);
        await session.RunAsync(
            "INSERT INTO items (id, name, price, category) VALUES (1, 'hammer', 9.5, 'tools'), (2, 'nails', NULL, 'tools'), (3, 'apple', 0.5, 'food')",
            cancellationToken: TestContext.Current.CancellationToken);
        return connection;
    }

    [Fact]
    public async Task Should_MaterializeRowsIncludingNulls_When_GeneratedQueryExecutes()
    {
        await using var connection = await OpenSeededConnectionAsync();

        var items = await ItemQueries.GetByCategoryAsync(
            connection, "tools", TestContext.Current.CancellationToken);

        Assert.Equal(
            [new Item(1, "hammer", 9.5m), new Item(2, "nails", null)],
            items);
    }

    [Fact]
    public async Task Should_ReturnSingleRow_When_ExactlyOneRowMatches()
    {
        await using var connection = await OpenSeededConnectionAsync();

        var item = await ItemQueries.GetByIdAsync(connection, 1, TestContext.Current.CancellationToken);

        Assert.Equal(new Item(1, "hammer", 9.5m), item);
    }

    [Fact]
    public async Task Should_ThrowInvalidOperationException_When_SingleRowQueryMatchesNothing()
    {
        await using var connection = await OpenSeededConnectionAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ItemQueries.GetByIdAsync(connection, 99, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ReturnNull_When_OptionalRowQueryMatchesNothing()
    {
        await using var connection = await OpenSeededConnectionAsync();

        var item = await ItemQueries.FindByIdAsync(connection, 99, TestContext.Current.CancellationToken);

        Assert.Null(item);
    }

    [Fact]
    public async Task Should_ReturnScalarCount_When_ScalarQueryExecutes()
    {
        await using var connection = await OpenSeededConnectionAsync();

        var count = await ItemQueries.CountByCategoryAsync(
            connection, "tools", TestContext.Current.CancellationToken);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Should_ReturnScalarList_When_QuerySelectsSingleColumn()
    {
        await using var connection = await OpenSeededConnectionAsync();

        var names = await ItemQueries.GetNamesByCategoryAsync(
            connection, "tools", TestContext.Current.CancellationToken);

        Assert.Equal(["hammer", "nails"], names);
    }

    [Fact]
    public async Task Should_StreamRows_When_IteratedWithAwaitForeach()
    {
        await using var connection = await OpenSeededConnectionAsync();

        var names = new List<string>();
        await foreach (var item in ItemQueries.StreamByCategoryAsync(
            connection, "tools", TestContext.Current.CancellationToken))
        {
            names.Add(item.Name);
        }

        Assert.Equal(["hammer", "nails"], names);
    }

    [Fact]
    public async Task Should_ReturnRowsAffected_When_ExecuteStatementRuns()
    {
        await using var connection = await OpenSeededConnectionAsync();

        var affected = await ItemQueries.DeleteByCategoryAsync(
            connection, "tools", TestContext.Current.CancellationToken);

        Assert.Equal(2, affected);
        Assert.Equal(0, await ItemQueries.CountByCategoryAsync(
            connection, "tools", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_MaterializeThroughProperties_When_RowTypeIsMutableClass()
    {
        await using var connection = await OpenSeededConnectionAsync();

        var entities = await ItemEntityQueries.GetByCategoryAsync(
            connection, "tools", TestContext.Current.CancellationToken);

        Assert.Equal(2, entities.Count);
        Assert.Equal(1, entities[0].Id);
        Assert.Equal("hammer", entities[0].Name);
        Assert.Equal(9.5m, entities[0].Price);
        Assert.Null(entities[1].Price);
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
