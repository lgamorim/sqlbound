using SqlBound.Introspection;

namespace SqlBound.MySql.IntegrationTests;

public sealed class MySqlQueryDescriberColumnTests(MySqlFixture fixture)
{
    [Fact]
    public async Task Should_DescribeNameOrdinalTypeAndNullability_When_SelectReturnsColumns()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new MySqlQueryDescriber().DescribeAsync(
            connection, "SELECT id, name, price FROM items", TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new DescribedColumn(0, "id", "INT", "int", IsNullable: false),
                new DescribedColumn(1, "name", "VARCHAR", "string", IsNullable: false),
                new DescribedColumn(2, "price", "DECIMAL", "decimal", IsNullable: true),
            ],
            description.Columns);
    }

    [Fact]
    public async Task Should_MapEveryMappedType_When_SelectCoversWholeTypeMap()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new MySqlQueryDescriber().DescribeAsync(
            connection,
            """
            SELECT bool_col, tinyint_col, smallint_col, mediumint_col, int_col, bigint_col,
                   float_col, double_col, decimal_col, char_col, varchar_col, text_col,
                   blob_col, date_col, datetime_col, timestamp_col
            FROM every_type
            """,
            TestContext.Current.CancellationToken);

        string[] expected =
        [
            "bool", "byte", "short", "int", "int", "long",
            "float", "double", "decimal", "string", "string", "string",
            "byte[]", "global::System.DateTime", "global::System.DateTime", "global::System.DateTime",
        ];
        Assert.Equal(expected, description.Columns.Select(column => column.ClrTypeText));
    }

    [Fact]
    public async Task Should_ReturnNoColumns_When_StatementProducesNoResultSet()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new MySqlQueryDescriber().DescribeAsync(
            connection, "DELETE FROM items WHERE id = 0", TestContext.Current.CancellationToken);

        Assert.Empty(description.Columns);
    }

    [Fact]
    public async Task Should_DescribeComputedColumns_When_SelectListHasExpressions()
    {
        // MySQL's COM_STMT_PREPARE resolves a real type for computed and aggregate columns too
        // (confirmed empirically in the M12 spike), unlike SQLite - no column needs rejecting.
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new MySqlQueryDescriber().DescribeAsync(
            connection,
            "SELECT COUNT(*) AS total, id * 2 AS doubled FROM items GROUP BY id",
            TestContext.Current.CancellationToken);

        Assert.Equal(["long", "long"], description.Columns.Select(column => column.ClrTypeText));
    }

    [Fact]
    public async Task Should_ThrowDescribeException_When_ColumnTypeHasNoMapping()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        const string commandText = "SELECT unmapped_col FROM every_type";

        var exception = await Assert.ThrowsAsync<SqlBoundDescribeException>(
            () => new MySqlQueryDescriber().DescribeAsync(
                connection, commandText, TestContext.Current.CancellationToken));

        Assert.Contains("JSON", exception.Message);
        Assert.Equal(commandText, exception.CommandText);
    }

    [Fact]
    public async Task Should_ThrowOperationCanceled_When_CancellationAlreadyRequested()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new MySqlQueryDescriber().DescribeAsync(
                connection, "SELECT id FROM items", new CancellationToken(canceled: true)));
    }
}
