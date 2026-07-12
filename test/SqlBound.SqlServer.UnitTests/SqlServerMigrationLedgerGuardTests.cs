using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace SqlBound.SqlServer.UnitTests;

public sealed class SqlServerMigrationLedgerGuardTests
{
    [Fact]
    public async Task Should_ThrowArgumentException_When_EnsureCreatedGivenNonSqlConnection()
    {
        var ledger = new SqlServerMigrationLedger();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => ledger.EnsureCreatedAsync(new FakeDbConnection(), TestContext.Current.CancellationToken));

        Assert.Contains("SqlConnection", exception.Message);
        Assert.Equal("connection", exception.ParamName);
    }

    [Fact]
    public async Task Should_ThrowArgumentException_When_GetAppliedGivenNonSqlConnection()
    {
        var ledger = new SqlServerMigrationLedger();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => ledger.GetAppliedAsync(new FakeDbConnection(), TestContext.Current.CancellationToken));

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
