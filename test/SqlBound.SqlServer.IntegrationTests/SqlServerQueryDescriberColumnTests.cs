using SqlBound.Introspection;

namespace SqlBound.SqlServer.IntegrationTests;

public sealed class SqlServerQueryDescriberColumnTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Should_DescribeNameOrdinalTypeAndNullability_When_SelectReturnsColumns()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new SqlServerQueryDescriber().DescribeAsync(
            connection, "SELECT Id, Name, Price FROM dbo.Items", TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new DescribedColumn(0, "Id", "int", "int", IsNullable: false),
                new DescribedColumn(1, "Name", "nvarchar(50)", "string", IsNullable: false),
                new DescribedColumn(2, "Price", "decimal(18,2)", "decimal", IsNullable: true),
            ],
            description.Columns);
    }

    [Fact]
    public async Task Should_MapEveryMappedType_When_SelectCoversWholeTypeMap()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new SqlServerQueryDescriber().DescribeAsync(
            connection,
            """
            SELECT BitCol, TinyIntCol, SmallIntCol, IntCol, BigIntCol, RealCol, FloatCol,
                   DecimalCol, NumericCol, MoneyCol, SmallMoneyCol,
                   CharCol, VarCharCol, VarCharMaxCol, NCharCol, NVarCharCol, NVarCharMaxCol,
                   TextCol, NTextCol,
                   BinaryCol, VarBinaryCol, ImageCol, RowVersionCol,
                   GuidCol, DateCol, SmallDateTimeCol, DateTimeCol, DateTime2Col
            FROM dbo.EveryType
            """,
            TestContext.Current.CancellationToken);

        string[] expected =
        [
            "bool", "byte", "short", "int", "long", "float", "double",
            "decimal", "decimal", "decimal", "decimal",
            "string", "string", "string", "string", "string", "string",
            "string", "string",
            "byte[]", "byte[]", "byte[]", "byte[]",
            "global::System.Guid", "global::System.DateTime", "global::System.DateTime",
            "global::System.DateTime", "global::System.DateTime",
        ];
        Assert.Equal(expected, description.Columns.Select(column => column.ClrTypeText));
    }

    [Fact]
    public async Task Should_ReturnNoColumns_When_StatementProducesNoResultSet()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new SqlServerQueryDescriber().DescribeAsync(
            connection, "DELETE FROM dbo.Items WHERE Id = 0", TestContext.Current.CancellationToken);

        Assert.Empty(description.Columns);
    }

    [Fact]
    public async Task Should_ReturnEmptyColumnName_When_ExpressionHasNoAlias()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new SqlServerQueryDescriber().DescribeAsync(
            connection, "SELECT COUNT(*) FROM dbo.Items", TestContext.Current.CancellationToken);

        var column = Assert.Single(description.Columns);
        Assert.Equal(string.Empty, column.Name);
        Assert.Equal("int", column.ClrTypeText);
    }

    [Theory]
    [InlineData("SELECT VariantCol FROM dbo.EveryType", "sql_variant")]
    [InlineData("SELECT DateTimeOffsetCol FROM dbo.EveryType", "datetimeoffset")]
    public async Task Should_ThrowDescribeException_When_ColumnTypeHasNoMapping(
        string commandText, string sqlTypeName)
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var exception = await Assert.ThrowsAsync<SqlBoundDescribeException>(
            () => new SqlServerQueryDescriber().DescribeAsync(
                connection, commandText, TestContext.Current.CancellationToken));

        Assert.Contains(sqlTypeName, exception.Message);
        Assert.Equal(commandText, exception.CommandText);
    }

    [Fact]
    public async Task Should_ThrowOperationCanceled_When_CancellationAlreadyRequested()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new SqlServerQueryDescriber().DescribeAsync(
                connection, "SELECT Id FROM dbo.Items", new CancellationToken(canceled: true)));
    }
}
