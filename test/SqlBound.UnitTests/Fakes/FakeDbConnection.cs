using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace SqlBound.UnitTests.Fakes;

internal sealed class FakeDbConnection : DbConnection
{
    public ConnectionState StateOverride { get; set; } = ConnectionState.Open;

    public int ExecuteNonQueryResult { get; set; }

    public Exception? ExecuteException { get; set; }

    public FakeDbCommand? LastCreatedCommand { get; private set; }

    [AllowNull]
    public override string ConnectionString { get; set; } = string.Empty;

    public override string Database => string.Empty;

    public override string DataSource => string.Empty;

    public override string ServerVersion => string.Empty;

    public override ConnectionState State => StateOverride;

    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

    public override void Close() => StateOverride = ConnectionState.Closed;

    public override void Open() => StateOverride = ConnectionState.Open;

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        throw new NotSupportedException();

    protected override DbCommand CreateDbCommand()
    {
        var command = new FakeDbCommand
        {
            Connection = this,
            ExecuteNonQueryResult = ExecuteNonQueryResult,
            ExceptionToThrow = ExecuteException,
        };

        LastCreatedCommand = command;
        return command;
    }
}
