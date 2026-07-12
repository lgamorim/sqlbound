using SqlBound.Introspection;

namespace SqlBound.Sqlite.IntegrationTests;

public sealed class SqliteQueryDescriberColumnTests(SqliteFixture fixture)
{
    [Fact]
    public async Task Should_DescribeNameTypeAndNullability_When_SelectReturnsPlainColumns()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new SqliteQueryDescriber().DescribeAsync(
            connection, "SELECT id, name, price FROM items", TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new DescribedColumn(0, "id", "INTEGER", "int", IsNullable: false),
                new DescribedColumn(1, "name", "TEXT", "string", IsNullable: false),
                new DescribedColumn(2, "price", "REAL", "double", IsNullable: true),
            ],
            description.Columns);
    }

    [Fact]
    public async Task Should_MapEveryMappedType_When_SelectCoversWholeTypeMap()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new SqliteQueryDescriber().DescribeAsync(
            connection,
            """
            SELECT bool_col, tinyint_col, smallint_col, int_col, bigint_col, real_col,
                   decimal_col, text_col, blob_col, guid_col, date_col, datetime_col
            FROM every_type
            """,
            TestContext.Current.CancellationToken);

        string[] expected =
        [
            "bool", "byte", "short", "int", "long", "double",
            "decimal", "string", "byte[]", "global::System.Guid",
            "global::System.DateTime", "global::System.DateTime",
        ];
        Assert.Equal(expected, description.Columns.Select(column => column.ClrTypeText));
    }

    [Fact]
    public async Task Should_ReturnNoColumns_When_StatementProducesNoResultSet()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new SqliteQueryDescriber().DescribeAsync(
            connection, "DELETE FROM items WHERE id = 0", TestContext.Current.CancellationToken);

        Assert.Empty(description.Columns);
    }

    [Theory]
    [InlineData("SELECT COUNT(*) FROM items")]
    [InlineData("SELECT id * 2 FROM items")]
    [InlineData("SELECT CAST(id AS TEXT) FROM items")]
    public async Task Should_ThrowDescribeException_When_ColumnIsComputedExpression(string commandText)
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var exception = await Assert.ThrowsAsync<SqlBoundDescribeException>(
            () => new SqliteQueryDescriber().DescribeAsync(
                connection, commandText, TestContext.Current.CancellationToken));

        Assert.Equal(commandText, exception.CommandText);
        Assert.Contains("no declared type", exception.Message);
    }

    [Fact]
    public async Task Should_ThrowDescribeException_When_ColumnTypeHasNoMapping()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        const string commandText = "SELECT unmapped_col FROM every_type";

        var exception = await Assert.ThrowsAsync<SqlBoundDescribeException>(
            () => new SqliteQueryDescriber().DescribeAsync(
                connection, commandText, TestContext.Current.CancellationToken));

        Assert.Contains("SOMETYPE", exception.Message);
        Assert.Equal(commandText, exception.CommandText);
    }

    [Fact]
    public async Task Should_ThrowOperationCanceled_When_CancellationAlreadyRequested()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new SqliteQueryDescriber().DescribeAsync(
                connection, "SELECT id FROM items", new CancellationToken(canceled: true)));
    }
}
