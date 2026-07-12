using Microsoft.Data.SqlClient;
using SqlBound.SqlServer.IntegrationTests;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(SqlServerFixture))]

namespace SqlBound.SqlServer.IntegrationTests;

/// <summary>
/// Starts one SQL Server container (via Testcontainers) for the whole test assembly and seeds the
/// schema the describe tests introspect. When Docker is unavailable the tests skip locally with a
/// pointer to the cause, but fail hard in CI, where the container is required.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
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

                CREATE TABLE dbo.EveryType (
                    BitCol bit NOT NULL,
                    TinyIntCol tinyint NOT NULL,
                    SmallIntCol smallint NOT NULL,
                    IntCol int NOT NULL,
                    BigIntCol bigint NOT NULL,
                    RealCol real NOT NULL,
                    FloatCol float NOT NULL,
                    DecimalCol decimal(18,2) NOT NULL,
                    NumericCol numeric(10,4) NOT NULL,
                    MoneyCol money NOT NULL,
                    SmallMoneyCol smallmoney NOT NULL,
                    CharCol char(10) NOT NULL,
                    VarCharCol varchar(50) NOT NULL,
                    VarCharMaxCol varchar(max) NOT NULL,
                    NCharCol nchar(10) NOT NULL,
                    NVarCharCol nvarchar(50) NOT NULL,
                    NVarCharMaxCol nvarchar(max) NOT NULL,
                    TextCol text NOT NULL,
                    NTextCol ntext NOT NULL,
                    BinaryCol binary(16) NOT NULL,
                    VarBinaryCol varbinary(max) NOT NULL,
                    ImageCol image NOT NULL,
                    RowVersionCol rowversion NOT NULL,
                    GuidCol uniqueidentifier NOT NULL,
                    DateCol date NOT NULL,
                    SmallDateTimeCol smalldatetime NOT NULL,
                    DateTimeCol datetime NOT NULL,
                    DateTime2Col datetime2 NOT NULL,
                    VariantCol sql_variant NULL,
                    DateTimeOffsetCol datetimeoffset NULL);
                """;
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception exception)
        {
            _startupFailure = exception;
        }
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public async Task<SqlConnection> OpenConnectionAsync()
    {
        var connection = new SqlConnection(GetConnectionString());
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
                    "The SQL Server container is required in CI.", _startupFailure);
            }

            Assert.Skip($"SQL Server container unavailable (is Docker running?): {_startupFailure.Message}");
        }

        return _container.GetConnectionString();
    }
}
