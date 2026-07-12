using MySqlConnector;

namespace SqlBound.MySql.IntegrationTests;

/// <summary>
/// Exercises <see cref="MySqlDatabaseAdmin"/>: create and drop a uniquely named database. Creating
/// a database is an administrative operation, so these connect as <c>root</c> (Testcontainers sets
/// the root password to the same value as the default user). Unique names mean these need no
/// serialization with the ledger tests.
/// </summary>
public sealed class MySqlDatabaseAdminTests(MySqlFixture fixture)
{
    [Fact]
    public async Task Should_CreateThenDropDatabase_When_TargetNamed()
    {
        var name = $"sqlbound_admin_{Guid.NewGuid():N}";
        var admin = new MySqlDatabaseAdmin();

        await admin.CreateAsync(RootConnectionStringFor(name), Token);
        Assert.True(await DatabaseExistsAsync(name));

        await admin.DropAsync(RootConnectionStringFor(name), Token);
        Assert.False(await DatabaseExistsAsync(name));
    }

    [Fact]
    public async Task Should_NotThrow_When_CreateCalledForExistingDatabase()
    {
        var name = $"sqlbound_admin_{Guid.NewGuid():N}";
        var admin = new MySqlDatabaseAdmin();

        try
        {
            await admin.CreateAsync(RootConnectionStringFor(name), Token);
            await admin.CreateAsync(RootConnectionStringFor(name), Token);
            Assert.True(await DatabaseExistsAsync(name));
        }
        finally
        {
            await admin.DropAsync(RootConnectionStringFor(name), Token);
        }
    }

    [Fact]
    public async Task Should_NotThrow_When_DropCalledForMissingDatabase()
    {
        await new MySqlDatabaseAdmin().DropAsync(RootConnectionStringFor($"sqlbound_admin_{Guid.NewGuid():N}"), Token);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string RootConnectionStringFor(string? database) =>
        new MySqlConnectionStringBuilder(fixture.GetConnectionString())
        {
            UserID = "root",
            Database = database ?? string.Empty,
        }.ConnectionString;

    private async Task<bool> DatabaseExistsAsync(string name)
    {
        await using var connection = new MySqlConnection(RootConnectionStringFor(null));
        await connection.OpenAsync(Token);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = @name;";
        command.Parameters.AddWithValue("@name", name);
        return Convert.ToInt64(await command.ExecuteScalarAsync(Token)) > 0;
    }
}
