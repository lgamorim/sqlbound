using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using Dapper;
using Microsoft.Data.Sqlite;

namespace SqlBound.Benchmarks;

/// <summary>
/// Compares SqlBound's generated code against Dapper and hand-written ADO.NET (the ceiling) on
/// identical work over in-memory SQLite. Absolute numbers are dominated by SQLite itself; the
/// meaningful signal is the relative time and allocation across the three approaches.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class QueryBenchmarks
{
    private const int RowCount = 1_000;
    private SqliteConnection _connection = null!;

    [GlobalSetup]
    public void Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var create = _connection.CreateCommand();
        create.CommandText =
            "CREATE TABLE items (id INTEGER PRIMARY KEY, name TEXT NOT NULL, price REAL NULL, category TEXT NOT NULL)";
        create.ExecuteNonQuery();

        using var transaction = _connection.BeginTransaction();
        for (var i = 1; i <= RowCount; i++)
        {
            using var insert = _connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO items (id, name, price, category) VALUES (@id, @name, @price, @category)";
            insert.Parameters.Add(new SqliteParameter("@id", i));
            insert.Parameters.Add(new SqliteParameter("@name", $"item-{i}"));
            insert.Parameters.Add(new SqliteParameter("@price", i % 10 == 0 ? DBNull.Value : i * 0.5));
            insert.Parameters.Add(new SqliteParameter("@category", i % 2 == 0 ? "even" : "odd"));
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    [GlobalCleanup]
    public void Cleanup() => _connection.Dispose();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("List1000")]
    public async Task<IReadOnlyList<Item>> RawAdoNet_List()
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT id AS Id, name AS Name, price AS Price FROM items";
        await using var reader = await command.ExecuteReaderAsync();
        var idOrdinal = reader.GetOrdinal("Id");
        var nameOrdinal = reader.GetOrdinal("Name");
        var priceOrdinal = reader.GetOrdinal("Price");
        var items = new List<Item>();
        while (await reader.ReadAsync())
        {
            items.Add(new Item(
                reader.GetInt64(idOrdinal),
                reader.GetString(nameOrdinal),
                reader.IsDBNull(priceOrdinal) ? null : reader.GetDouble(priceOrdinal)));
        }

        return items;
    }

    [Benchmark]
    [BenchmarkCategory("List1000")]
    public Task<IReadOnlyList<Item>> SqlBound_List() => BenchmarkQueries.GetAllAsync(_connection);

    [Benchmark]
    [BenchmarkCategory("List1000")]
    public async Task<List<Item>> Dapper_List() =>
        (await _connection.QueryAsync<Item>("SELECT id AS Id, name AS Name, price AS Price FROM items")).AsList();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SingleRow")]
    public async Task<Item> RawAdoNet_Single()
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT id AS Id, name AS Name, price AS Price FROM items WHERE id = @id";
        command.Parameters.Add(new SqliteParameter("@id", 500));
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("No row.");
        }

        return new Item(
            reader.GetInt64(reader.GetOrdinal("Id")),
            reader.GetString(reader.GetOrdinal("Name")),
            reader.IsDBNull(reader.GetOrdinal("Price")) ? null : reader.GetDouble(reader.GetOrdinal("Price")));
    }

    [Benchmark]
    [BenchmarkCategory("SingleRow")]
    public Task<Item> SqlBound_Single() => BenchmarkQueries.GetByIdAsync(_connection, 500);

    [Benchmark]
    [BenchmarkCategory("SingleRow")]
    public Task<Item> Dapper_Single() =>
        _connection.QuerySingleAsync<Item>(
            "SELECT id AS Id, name AS Name, price AS Price FROM items WHERE id = @id", new { id = 500 });

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Scalar")]
    public async Task<int> RawAdoNet_Scalar()
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM items";
        var value = await command.ExecuteScalarAsync();
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    [Benchmark]
    [BenchmarkCategory("Scalar")]
    public Task<int> SqlBound_Scalar() => BenchmarkQueries.CountAsync(_connection);

    [Benchmark]
    [BenchmarkCategory("Scalar")]
    public Task<int> Dapper_Scalar() => _connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM items");

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Execute")]
    public async Task<int> RawAdoNet_Execute()
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "UPDATE items SET price = @price WHERE id = @id";
        command.Parameters.Add(new SqliteParameter("@price", 1.25));
        command.Parameters.Add(new SqliteParameter("@id", 500));
        return await command.ExecuteNonQueryAsync();
    }

    [Benchmark]
    [BenchmarkCategory("Execute")]
    public Task<int> SqlBound_Execute() => BenchmarkQueries.RepriceAsync(_connection, 500, 1.25m);

    [Benchmark]
    [BenchmarkCategory("Execute")]
    public Task<int> Dapper_Execute() =>
        _connection.ExecuteAsync(
            "UPDATE items SET price = @price WHERE id = @id", new { price = 1.25m, id = 500 });
}
