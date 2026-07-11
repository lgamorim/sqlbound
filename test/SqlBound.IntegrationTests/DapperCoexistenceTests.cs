using Dapper;
using Microsoft.Data.Sqlite;

namespace SqlBound.IntegrationTests;

public class DapperCoexistenceTests
{
    [Fact]
    public async Task Should_ProduceConsistentResults_When_InterleavingSqlBoundAndDapperOnSameConnection()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var session = new SqlSession(connection);

        await session.RunAsync(
            "CREATE TABLE items (id INTEGER PRIMARY KEY, name TEXT NOT NULL)",
            cancellationToken: TestContext.Current.CancellationToken);

        await session.RunAsync(
            "INSERT INTO items (id, name) VALUES (@id, @name)",
            new SqlParameters(("@id", 1), ("@name", "from-sqlbound")),
            TestContext.Current.CancellationToken);

        var readByDapper = await connection.QuerySingleAsync<string>(
            "SELECT name FROM items WHERE id = @id", new { id = 1 });
        Assert.Equal("from-sqlbound", readByDapper);

        await connection.ExecuteAsync(
            "INSERT INTO items (id, name) VALUES (@id, @name)", new { id = 2, name = "from-dapper" });

        var readBySqlBound = await session.FetchScalarAsync<string>(
            "SELECT name FROM items WHERE id = @id",
            new SqlParameters(("@id", 2)),
            TestContext.Current.CancellationToken);
        Assert.Equal("from-dapper", readBySqlBound);
    }

    [Fact]
    public async Task Should_PersistBothWrites_When_SharedTransactionCommits()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var session = new SqlSession(connection);

        await session.RunAsync(
            "CREATE TABLE items (id INTEGER PRIMARY KEY, name TEXT NOT NULL)",
            cancellationToken: TestContext.Current.CancellationToken);

        await using (var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            var transactionalSession = new SqlSession(connection, transaction);

            await transactionalSession.RunAsync(
                "INSERT INTO items (id, name) VALUES (@id, @name)",
                new SqlParameters(("@id", 1), ("@name", "from-sqlbound")),
                TestContext.Current.CancellationToken);

            await connection.ExecuteAsync(
                "INSERT INTO items (id, name) VALUES (@id, @name)",
                new { id = 2, name = "from-dapper" },
                transaction);

            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM items");
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Should_DiscardBothWrites_When_SharedTransactionRollsBack()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var session = new SqlSession(connection);

        await session.RunAsync(
            "CREATE TABLE items (id INTEGER PRIMARY KEY, name TEXT NOT NULL)",
            cancellationToken: TestContext.Current.CancellationToken);

        await using (var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            var transactionalSession = new SqlSession(connection, transaction);

            await transactionalSession.RunAsync(
                "INSERT INTO items (id, name) VALUES (@id, @name)",
                new SqlParameters(("@id", 1), ("@name", "from-sqlbound")),
                TestContext.Current.CancellationToken);

            await connection.ExecuteAsync(
                "INSERT INTO items (id, name) VALUES (@id, @name)",
                new { id = 2, name = "from-dapper" },
                transaction);

            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM items");
        Assert.Equal(0, count);
    }
}
