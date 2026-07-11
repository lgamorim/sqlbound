using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace SqlBound.UnitTests.Fakes;

internal sealed class FakeDbCommand : DbCommand
{
    private readonly FakeDbParameterCollection _parameters = new();

    [AllowNull]
    public override string CommandText { get; set; } = string.Empty;

    public override int CommandTimeout { get; set; }

    public override CommandType CommandType { get; set; } = CommandType.Text;

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; }

    public int ExecuteNonQueryResult { get; set; }

    public object? ExecuteScalarResult { get; set; }

    public Exception? ExceptionToThrow { get; set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    protected override DbConnection? DbConnection { get; set; }

    protected override DbParameterCollection DbParameterCollection => _parameters;

    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel()
    {
    }

    public override int ExecuteNonQuery()
    {
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return ExecuteNonQueryResult;
    }

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
    {
        ReceivedCancellationToken = cancellationToken;

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<int>(cancellationToken);
        }

        if (ExceptionToThrow is not null)
        {
            return Task.FromException<int>(ExceptionToThrow);
        }

        return Task.FromResult(ExecuteNonQueryResult);
    }

    public override object? ExecuteScalar()
    {
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        return ExecuteScalarResult;
    }

    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
    {
        ReceivedCancellationToken = cancellationToken;

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<object?>(cancellationToken);
        }

        if (ExceptionToThrow is not null)
        {
            return Task.FromException<object?>(ExceptionToThrow);
        }

        return Task.FromResult(ExecuteScalarResult);
    }

    public override void Prepare()
    {
    }

    protected override DbParameter CreateDbParameter() => new FakeDbParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
        throw new NotSupportedException();
}
