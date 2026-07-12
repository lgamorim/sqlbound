using MySqlConnector;
using SqlBound.MySql.IntegrationTests;
using Testcontainers.MySql;

[assembly: AssemblyFixture(typeof(MySqlFixture))]

namespace SqlBound.MySql.IntegrationTests;

/// <summary>
/// Starts one MySQL container (via Testcontainers) for the whole test assembly and seeds the
/// schema the describe tests introspect. When Docker is unavailable the tests skip locally with a
/// pointer to the cause, but fail hard in CI, where the container is required.
/// </summary>
public sealed class MySqlFixture : IAsyncLifetime
{
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.4").Build();
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

                CREATE TABLE every_type (
                    bool_col BOOLEAN NOT NULL,
                    tinyint_col TINYINT NOT NULL,
                    smallint_col SMALLINT NOT NULL,
                    mediumint_col MEDIUMINT NOT NULL,
                    int_col INT NOT NULL,
                    bigint_col BIGINT NOT NULL,
                    float_col FLOAT NOT NULL,
                    double_col DOUBLE NOT NULL,
                    decimal_col DECIMAL(18,2) NOT NULL,
                    char_col CHAR(10) NOT NULL,
                    varchar_col VARCHAR(50) NOT NULL,
                    text_col TEXT NOT NULL,
                    blob_col BLOB NOT NULL,
                    date_col DATE NOT NULL,
                    datetime_col DATETIME NOT NULL,
                    timestamp_col TIMESTAMP NULL,
                    unmapped_col JSON NULL);
                """;
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _startupFailure = exception;
        }
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public async Task<MySqlConnection> OpenConnectionAsync()
    {
        var connection = new MySqlConnection(GetConnectionString());
        await connection.OpenAsync();
        return connection;
    }

    public string GetConnectionString()
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

        return _container.GetConnectionString();
    }
}
