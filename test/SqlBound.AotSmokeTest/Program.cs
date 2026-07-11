using Microsoft.Data.Sqlite;
using SqlBound;
using SqlBound.AotSmokeTest;

await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();

var session = new SqlSession(connection);
await session.RunAsync(
    "CREATE TABLE items (id INTEGER PRIMARY KEY, name TEXT NOT NULL, price REAL NULL, category TEXT NOT NULL)");
await session.RunAsync(
    "INSERT INTO items (id, name, price, category) VALUES (1, 'hammer', 9.5, 'tools'), (2, 'nails', NULL, 'tools'), (3, 'apple', 0.5, 'food')");

var tools = await ItemQueries.GetByCategoryAsync(connection, "tools");
Verify(tools.Count == 2 && tools[0] == new Item(1, "hammer", 9.5m) && tools[1].Price is null, "row list");

var single = await ItemQueries.GetByIdAsync(connection, 1);
Verify(single.Name == "hammer", "single row");

Verify(await ItemQueries.FindByIdAsync(connection, 99) is null, "optional row miss");

Verify(await ItemQueries.CountByCategoryAsync(connection, "tools") == 2, "scalar");

var names = await ItemQueries.GetNamesByCategoryAsync(connection, "tools");
Verify(names.Count == 2 && names[0] == "hammer" && names[1] == "nails", "scalar list");

var streamed = 0;
await foreach (var item in ItemQueries.StreamByCategoryAsync(connection, "tools"))
{
    streamed++;
}

Verify(streamed == 2, "streaming");

var entities = await ItemEntityQueries.GetByCategoryAsync(connection, "tools");
Verify(entities.Count == 2 && entities[0].Name == "hammer" && entities[1].Price is null, "property-mapped rows");

Verify(await ItemQueries.DeleteByCategoryAsync(connection, "food") == 1, "execute rows affected");

Verify(await session.FetchScalarAsync<long>("SELECT COUNT(*) FROM items") == 2, "SqlSession scalar");

Console.WriteLine("Smoke test passed: every query shape executed.");
return 0;

static void Verify(bool condition, string check)
{
    if (!condition)
    {
        Console.Error.WriteLine($"AOT smoke test FAILED: {check}");
        Environment.Exit(1);
    }
}
