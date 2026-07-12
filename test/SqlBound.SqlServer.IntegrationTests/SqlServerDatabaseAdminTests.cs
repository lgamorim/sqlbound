using Microsoft.Data.SqlClient;

namespace SqlBound.SqlServer.IntegrationTests;

/// <summary>
/// Exercises <see cref="SqlServerDatabaseAdmin"/> against the shared container. Each test targets a
/// uniquely named database so the create/drop lifecycle does not collide with any other test.
/// </summary>
public sealed class SqlServerDatabaseAdminTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Should_CreateThenDropDatabase_When_TargetNamed()
    {
        var name = UniqueDatabaseName();
        var connectionString = ConnectionStringFor(name);
        var admin = new SqlServerDatabaseAdmin();

        await admin.CreateAsync(connectionString, TestContext.Current.CancellationToken);
        Assert.True(await DatabaseExistsAsync(name));

        await admin.DropAsync(connectionString, TestContext.Current.CancellationToken);
        Assert.False(await DatabaseExistsAsync(name));
    }

    [Fact]
    public async Task Should_NotThrow_When_CreateCalledForExistingDatabase()
    {
        var name = UniqueDatabaseName();
        var connectionString = ConnectionStringFor(name);
        var admin = new SqlServerDatabaseAdmin();

        try
        {
            await admin.CreateAsync(connectionString, TestContext.Current.CancellationToken);
            await admin.CreateAsync(connectionString, TestContext.Current.CancellationToken);
            Assert.True(await DatabaseExistsAsync(name));
        }
        finally
        {
            await admin.DropAsync(connectionString, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Should_NotThrow_When_DropCalledForMissingDatabase()
    {
        var connectionString = ConnectionStringFor(UniqueDatabaseName());

        await new SqlServerDatabaseAdmin().DropAsync(connectionString, TestContext.Current.CancellationToken);
    }

    private static string UniqueDatabaseName() => $"sqlbound_admin_{Guid.NewGuid():N}";

    private string ConnectionStringFor(string database) =>
        new SqlConnectionStringBuilder(fixture.GetConnectionString()) { InitialCatalog = database }.ConnectionString;

    private async Task<bool> DatabaseExistsAsync(string name)
    {
        await using var connection = await fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DB_ID(@name);";
        command.Parameters.AddWithValue("@name", name);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) is not (null or DBNull);
    }
}
