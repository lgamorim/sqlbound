using Npgsql;
using Testcontainers.PostgreSql;

namespace SqlBound.Cli.IntegrationTests;

/// <summary>
/// One Postgres container for the prepare tests (class fixture, so tests that never touch a
/// database do not pay for it), seeded with the schema the sample queries describe against.
/// Skips locally without Docker, fails hard in CI — same policy as the SQL Server fixture.
/// </summary>
public sealed class PreparePostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithUsername("sqlbound")
        .WithPassword("sqlbound")
        .WithDatabase("sqlbound")
        .Build();

    private Exception? _startupFailure;

    public async ValueTask InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
            await using var connection = new NpgsqlConnection(_container.GetConnectionString());
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE items (
                    id integer NOT NULL PRIMARY KEY,
                    name text NOT NULL,
                    price numeric(18,2) NULL);
                """;
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _startupFailure = exception;
        }
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>The container's connection expressed as a <c>postgresql://</c> URL, so tests exercise <see cref="DatabaseUrl"/>'s scheme parsing.</summary>
    public string GetConnectionUrl()
    {
        if (_startupFailure is not null)
        {
            if (Environment.GetEnvironmentVariable("CI") is "true" or "1")
            {
                throw new InvalidOperationException(
                    "The Postgres container is required in CI.", _startupFailure);
            }

            Assert.Skip($"Postgres container unavailable (is Docker running?): {_startupFailure.Message}");
        }

        return $"postgresql://sqlbound:sqlbound@{_container.Hostname}:{_container.GetMappedPublicPort(5432)}/sqlbound";
    }
}
