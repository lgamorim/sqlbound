using global::Npgsql;
using SqlBound.Npgsql.IntegrationTests;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace SqlBound.Npgsql.IntegrationTests;

/// <summary>
/// Starts one Postgres container (via Testcontainers) for the whole test assembly and seeds the
/// schema the describe tests introspect. When Docker is unavailable the tests skip locally with a
/// pointer to the cause, but fail hard in CI, where the container is required.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
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

                CREATE TABLE every_type (
                    bool_col boolean NOT NULL,
                    smallint_col smallint NOT NULL,
                    int_col integer NOT NULL,
                    bigint_col bigint NOT NULL,
                    real_col real NOT NULL,
                    double_col double precision NOT NULL,
                    numeric_col numeric(18,2) NOT NULL,
                    char_col character(10) NOT NULL,
                    varchar_col character varying(50) NOT NULL,
                    text_col text NOT NULL,
                    bytea_col bytea NOT NULL,
                    uuid_col uuid NOT NULL,
                    date_col date NOT NULL,
                    timestamp_col timestamp without time zone NOT NULL,
                    timestamptz_col timestamp with time zone NULL,
                    json_col json NULL);
                """;
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _startupFailure = exception;
        }
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public async Task<NpgsqlConnection> OpenConnectionAsync()
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

        var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        return connection;
    }
}
