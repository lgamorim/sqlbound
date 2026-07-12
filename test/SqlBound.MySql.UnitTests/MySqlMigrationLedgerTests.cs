namespace SqlBound.MySql.UnitTests;

public sealed class MySqlMigrationLedgerTests
{
    [Fact]
    public void Should_ReportNonTransactionalDdl_Because_MySqlCommitsDdlImplicitly()
    {
        Assert.False(new MySqlMigrationLedger().SupportsTransactionalDdl);
    }
}
