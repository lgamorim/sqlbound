using global::Npgsql;

namespace SqlBound.Npgsql.IntegrationTests;

/// <summary>
/// Exercises <see cref="NpgsqlDatabaseAdmin"/>: create and drop a uniquely named database via the
/// maintenance connection. Unique names mean these need no serialization with the ledger tests.
/// </summary>
public sealed class NpgsqlDatabaseAdminTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Should_CreateThenDropDatabase_When_TargetNamed()
    {
        var name = $"sqlbound_admin_{Guid.NewGuid():N}";
        var connectionString = ConnectionStringFor(name);
        var admin = new NpgsqlDatabaseAdmin();

        await admin.CreateAsync(connectionString, Token);
        Assert.True(await DatabaseExistsAsync(name));

        await admin.DropAsync(connectionString, Token);
        Assert.False(await DatabaseExistsAsync(name));
    }

    [Fact]
    public async Task Should_NotThrow_When_CreateCalledForExistingDatabase()
    {
        var name = $"sqlbound_admin_{Guid.NewGuid():N}";
        var connectionString = ConnectionStringFor(name);
        var admin = new NpgsqlDatabaseAdmin();

        try
        {
            await admin.CreateAsync(connectionString, Token);
            await admin.CreateAsync(connectionString, Token);
            Assert.True(await DatabaseExistsAsync(name));
        }
        finally
        {
            await admin.DropAsync(connectionString, Token);
        }
    }

    [Fact]
    public async Task Should_NotThrow_When_DropCalledForMissingDatabase()
    {
        await new NpgsqlDatabaseAdmin().DropAsync(ConnectionStringFor($"sqlbound_admin_{Guid.NewGuid():N}"), Token);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(fixture.GetConnectionString()) { Database = database }.ConnectionString;

    private async Task<bool> DatabaseExistsAsync(string name)
    {
        await using var connection = await fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name;";
        command.Parameters.AddWithValue("@name", name);
        return await command.ExecuteScalarAsync(Token) is not null;
    }
}
