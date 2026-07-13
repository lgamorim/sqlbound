using System.Data;
using SqlBound.UnitTests.Fakes;

namespace SqlBound.UnitTests;

public class SqlSessionTests
{
    [Fact]
    public async Task Should_ReturnAffectedRowCount_When_ExecutingNonQuery()
    {
        var connection = new FakeDbConnection { ExecuteNonQueryResult = 3 };
        var session = new SqlSession(connection);

        var affected = await session.RunAsync("UPDATE t SET x = 1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, affected);
    }

    [Fact]
    public async Task Should_SetCommandText_When_ExecutingNonQuery()
    {
        var connection = new FakeDbConnection();
        var session = new SqlSession(connection);

        await session.RunAsync("DELETE FROM t", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("DELETE FROM t", connection.LastCreatedCommand!.CommandText);
    }

    [Fact]
    public async Task Should_BindParameters_When_ParametersGiven()
    {
        var connection = new FakeDbConnection();
        var session = new SqlSession(connection);
        var parameters = new SqlParameters(("id", 42));

        await session.RunAsync("DELETE FROM t WHERE id = @id", parameters, TestContext.Current.CancellationToken);

        var command = connection.LastCreatedCommand!;
        Assert.Single(command.Parameters);
        Assert.Equal("id", command.Parameters[0].ParameterName);
        Assert.Equal(42, command.Parameters[0].Value);
    }

    [Fact]
    public async Task Should_BindDBNull_When_ParameterValueIsNull()
    {
        var connection = new FakeDbConnection();
        var session = new SqlSession(connection);
        var parameters = new SqlParameters(("name", null));

        await session.RunAsync("UPDATE t SET name = @name", parameters, TestContext.Current.CancellationToken);

        Assert.Equal(DBNull.Value, connection.LastCreatedCommand!.Parameters[0].Value);
    }

    [Fact]
    public async Task Should_PassCancellationTokenToCommand_When_TokenProvided()
    {
        var connection = new FakeDbConnection();
        var session = new SqlSession(connection);
        using var cts = new CancellationTokenSource();

        await session.RunAsync("DELETE FROM t", cancellationToken: cts.Token);

        Assert.Equal(cts.Token, connection.LastCreatedCommand!.ReceivedCancellationToken);
    }

    [Fact]
    public async Task Should_ThrowOperationCanceledException_When_TokenAlreadyCanceled()
    {
        var connection = new FakeDbConnection();
        var session = new SqlSession(connection);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => session.RunAsync("DELETE FROM t", cancellationToken: cts.Token));

        Assert.Null(connection.LastCreatedCommand);
    }

    [Fact]
    public async Task Should_ThrowInvalidOperationException_When_ConnectionIsClosed()
    {
        var connection = new FakeDbConnection { StateOverride = ConnectionState.Closed };
        var session = new SqlSession(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.RunAsync("DELETE FROM t", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowArgumentException_When_SqlIsEmpty()
    {
        var connection = new FakeDbConnection();
        var session = new SqlSession(connection);

        await Assert.ThrowsAsync<ArgumentException>(
            () => session.RunAsync("", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowArgumentException_When_SqlIsWhitespace()
    {
        var connection = new FakeDbConnection();
        var session = new SqlSession(connection);

        await Assert.ThrowsAsync<ArgumentException>(
            () => session.RunAsync("   ", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Should_ThrowArgumentNullException_When_ConnectionIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SqlSession(null!));
    }

    [Fact]
    public async Task Should_PropagateProviderException_When_CommandFails()
    {
        var providerException = new InvalidOperationException("boom");
        var connection = new FakeDbConnection { ExecuteException = providerException };
        var session = new SqlSession(connection);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.RunAsync("DELETE FROM t", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(providerException, thrown);
    }

    [Fact]
    public async Task Should_ReturnValue_When_ScalarResultMatchesRequestedType()
    {
        var connection = new FakeDbConnection { ExecuteScalarResult = 42 };
        var session = new SqlSession(connection);

        var result = await session.FetchScalarAsync<int>(
            "SELECT COUNT(*) FROM t", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task Should_ConvertValue_When_ScalarResultTypeDiffersFromRequestedType()
    {
        var connection = new FakeDbConnection { ExecuteScalarResult = 42L };
        var session = new SqlSession(connection);

        var result = await session.FetchScalarAsync<int>(
            "SELECT COUNT(*) FROM t", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task Should_ReturnNull_When_ResultIsDBNullAndTypeIsNullableValueType()
    {
        var connection = new FakeDbConnection { ExecuteScalarResult = DBNull.Value };
        var session = new SqlSession(connection);

        var result = await session.FetchScalarAsync<int?>(
            "SELECT MAX(x) FROM t", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task Should_ReturnNull_When_ResultIsNullAndTypeIsReferenceType()
    {
        var connection = new FakeDbConnection { ExecuteScalarResult = null };
        var session = new SqlSession(connection);

        var result = await session.FetchScalarAsync<string>(
            "SELECT name FROM t WHERE 1 = 0", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task Should_ThrowInvalidOperationException_When_ResultIsDBNullAndTypeIsNonNullableValueType()
    {
        var connection = new FakeDbConnection { ExecuteScalarResult = DBNull.Value };
        var session = new SqlSession(connection);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.FetchScalarAsync<int>("SELECT MAX(x) FROM t", cancellationToken: TestContext.Current.CancellationToken));

        // The same code path covers both an empty result set and a NULL value; the message
        // must not claim "no value" when the database may well have returned a NULL.
        Assert.Contains("NULL", exception.Message);
    }

    [Fact]
    public async Task Should_ThrowInvalidOperationException_When_ScalarResultCannotConvertToRequestedType()
    {
        var connection = new FakeDbConnection { ExecuteScalarResult = "0e984725-c51c-4bf4-9960-e1c80e27aba0" };
        var session = new SqlSession(connection);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.FetchScalarAsync<Guid>("SELECT id FROM t", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains(nameof(String), exception.Message);
        Assert.Contains(nameof(Guid), exception.Message);
        Assert.IsType<InvalidCastException>(exception.InnerException);
    }

    [Fact]
    public async Task Should_ThrowInvalidOperationException_When_FetchingScalarOnClosedConnection()
    {
        var connection = new FakeDbConnection { StateOverride = ConnectionState.Closed };
        var session = new SqlSession(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.FetchScalarAsync<int>("SELECT 1", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_ThrowArgumentException_When_FetchingScalarWithEmptySql()
    {
        var connection = new FakeDbConnection();
        var session = new SqlSession(connection);

        await Assert.ThrowsAsync<ArgumentException>(
            () => session.FetchScalarAsync<int>("", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_PassCancellationTokenToCommand_When_FetchingScalarWithTokenProvided()
    {
        var connection = new FakeDbConnection { ExecuteScalarResult = 1 };
        var session = new SqlSession(connection);
        using var cts = new CancellationTokenSource();

        await session.FetchScalarAsync<int>("SELECT 1", cancellationToken: cts.Token);

        Assert.Equal(cts.Token, connection.LastCreatedCommand!.ReceivedCancellationToken);
    }

    [Fact]
    public async Task Should_ThrowOperationCanceledException_When_FetchingScalarWithAlreadyCanceledToken()
    {
        var connection = new FakeDbConnection();
        var session = new SqlSession(connection);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => session.FetchScalarAsync<int>("SELECT 1", cancellationToken: cts.Token));

        Assert.Null(connection.LastCreatedCommand);
    }

    [Fact]
    public async Task Should_PropagateProviderException_When_ScalarCommandFails()
    {
        var providerException = new InvalidOperationException("boom");
        var connection = new FakeDbConnection { ExecuteException = providerException };
        var session = new SqlSession(connection);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.FetchScalarAsync<int>("SELECT 1", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Same(providerException, thrown);
    }
}
