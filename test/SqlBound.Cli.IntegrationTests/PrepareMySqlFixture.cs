using MySqlConnector;
using Testcontainers.MySql;

namespace SqlBound.Cli.IntegrationTests;

/// <summary>
/// One MySQL container for the prepare tests (class fixture, so tests that never touch a
/// database do not pay for it), seeded with the schema the sample queries describe against.
/// Skips locally without Docker, fails hard in CI — same policy as the other prepare fixtures.
/// </summary>
public sealed class PrepareMySqlFixture : IAsyncLifetime
{
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.4")
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
            await using var connection = new MySqlConnection(_container.GetConnectionString());
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE items (
                    id INT NOT NULL PRIMARY KEY,
                    name VARCHAR(50) NOT NULL,
                    price DECIMAL(18,2) NULL);
                """;
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _startupFailure = exception;
        }
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>The container's connection expressed as a <c>mysql://</c> URL, so tests exercise <see cref="DatabaseUrl"/>'s scheme parsing.</summary>
    public string GetConnectionUrl()
    {
        if (_startupFailure is not null)
        {
            if (Environment.GetEnvironmentVariable("CI") is "true" or "1")
            {
                throw new InvalidOperationException(
                    "The MySQL container is required in CI.", _startupFailure);
            }

            Assert.Skip($"MySQL container unavailable (is Docker running?): {_startupFailure.Message}");
        }

        return $"mysql://sqlbound:sqlbound@{_container.Hostname}:{_container.GetMappedPublicPort(3306)}/sqlbound";
    }
}
