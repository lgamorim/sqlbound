using SqlBound.Introspection;

namespace SqlBound.Npgsql.IntegrationTests;

public sealed class NpgsqlQueryDescriberColumnTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Should_DescribeNameOrdinalTypeAndNullability_When_SelectReturnsColumns()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new NpgsqlQueryDescriber().DescribeAsync(
            connection, "SELECT id, name, price FROM items", TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new DescribedColumn(0, "id", "integer", "int", IsNullable: false),
                new DescribedColumn(1, "name", "text", "string", IsNullable: false),
                new DescribedColumn(2, "price", "numeric", "decimal", IsNullable: true),
            ],
            description.Columns);
    }

    [Fact]
    public async Task Should_MapEveryMappedType_When_SelectCoversWholeTypeMap()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new NpgsqlQueryDescriber().DescribeAsync(
            connection,
            """
            SELECT bool_col, smallint_col, int_col, bigint_col, real_col, double_col,
                   numeric_col, char_col, varchar_col, text_col, bytea_col, uuid_col,
                   date_col, timestamp_col
            FROM every_type
            """,
            TestContext.Current.CancellationToken);

        string[] expected =
        [
            "bool", "short", "int", "long", "float", "double",
            "decimal", "string", "string", "string", "byte[]", "global::System.Guid",
            "global::System.DateTime", "global::System.DateTime",
        ];
        Assert.Equal(expected, description.Columns.Select(column => column.ClrTypeText));
    }

    [Fact]
    public async Task Should_ReturnNoColumns_When_StatementProducesNoResultSet()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new NpgsqlQueryDescriber().DescribeAsync(
            connection, "DELETE FROM items WHERE id = 0", TestContext.Current.CancellationToken);

        Assert.Empty(description.Columns);
    }

    [Fact]
    public async Task Should_DescribeComputedColumns_When_SelectListHasExpressions()
    {
        // Unlike SQLite, Postgres's Describe message resolves a real type for computed and
        // aggregate columns (confirmed empirically in the M11 spike) - it just can't resolve
        // nullability for them, since they have no source table to check against.
        await using var connection = await fixture.OpenConnectionAsync();

        var description = await new NpgsqlQueryDescriber().DescribeAsync(
            connection,
            "SELECT COUNT(*) AS total, id * 2 AS doubled FROM items GROUP BY id",
            TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new DescribedColumn(0, "total", "bigint", "long", IsNullable: true),
                new DescribedColumn(1, "doubled", "integer", "int", IsNullable: true),
            ],
            description.Columns);
    }

    [Fact]
    public async Task Should_ThrowDescribeException_When_ColumnTypeHasNoMapping()
    {
        await using var connection = await fixture.OpenConnectionAsync();
        const string commandText = "SELECT timestamptz_col FROM every_type";

        var exception = await Assert.ThrowsAsync<SqlBoundDescribeException>(
            () => new NpgsqlQueryDescriber().DescribeAsync(
                connection, commandText, TestContext.Current.CancellationToken));

        Assert.Contains("timestamp with time zone", exception.Message);
        Assert.Equal(commandText, exception.CommandText);
    }

    [Fact]
    public async Task Should_ThrowOperationCanceled_When_CancellationAlreadyRequested()
    {
        await using var connection = await fixture.OpenConnectionAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new NpgsqlQueryDescriber().DescribeAsync(
                connection, "SELECT id FROM items", new CancellationToken(canceled: true)));
    }
}
