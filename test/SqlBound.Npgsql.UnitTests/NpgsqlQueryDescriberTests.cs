using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using SqlBound.Introspection;

namespace SqlBound.Npgsql.UnitTests;

public sealed class NpgsqlQueryDescriberTests
{
    [Fact]
    public async Task Should_ThrowArgumentException_When_ConnectionIsNotNpgsqlConnection()
    {
        IQueryDescriber describer = new NpgsqlQueryDescriber();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => describer.DescribeAsync(new FakeDbConnection(), "SELECT 1", TestContext.Current.CancellationToken));

        Assert.Contains("NpgsqlConnection", exception.Message);
        Assert.Equal("connection", exception.ParamName);
    }

    private sealed class FakeDbConnection : DbConnection
    {
        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => string.Empty;

        public override string DataSource => string.Empty;

        public override string ServerVersion => string.Empty;

        public override ConnectionState State => ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName)
        {
        }

        public override void Close()
        {
        }

        public override void Open()
        {
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }
}
