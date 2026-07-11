using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace SqlBound.Cli.IntegrationTests;

/// <summary>
/// One SQL Server container for the prepare tests (class fixture, so tests that never touch a
/// database do not pay for it), seeded with the schema the sample queries describe against.
/// Skips locally without Docker, fails hard in CI — same policy as the M7 describe suite.
/// </summary>
public sealed class PrepareSqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private Exception? _startupFailure;

    public async ValueTask InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
            await using var connection = new SqlConnection(_container.GetConnectionString());
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE dbo.Items (
                    Id int NOT NULL PRIMARY KEY,
                    Name nvarchar(50) NOT NULL,
                    Price decimal(18,2) NULL);
                """;
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _startupFailure = exception;
        }
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public string GetConnectionString()
    {
        if (_startupFailure is not null)
        {
            if (Environment.GetEnvironmentVariable("CI") is "true" or "1")
            {
                throw new InvalidOperationException(
                    "The SQL Server container is required in CI.", _startupFailure);
            }

            Assert.Skip($"SQL Server container unavailable (is Docker running?): {_startupFailure.Message}");
        }

        return _container.GetConnectionString();
    }
}
