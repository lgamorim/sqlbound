using SqlBound.Introspection;

namespace SqlBound.Cli.UnitTests;

public sealed class SnapshotWriterTests
{
    private static readonly QueryDescription Description = new(
        [
            new DescribedColumn(0, "Id", "int", "int", IsNullable: false),
            new DescribedColumn(1, "Name", "nvarchar(50)", "string", IsNullable: true),
        ],
        [new DescribedParameter("id", "int", "int")]);

    [Fact]
    public void Should_SerializeDeterministically_When_DescriptionIsComplete()
    {
        const string expected =
            """
            {
              "commandText": "SELECT Id, Name FROM dbo.Items WHERE Id = @id",
              "provider": "sqlserver",
              "columns": [
                { "ordinal": 0, "name": "Id", "sqlTypeName": "int", "clrTypeText": "int", "isNullable": false },
                { "ordinal": 1, "name": "Name", "sqlTypeName": "nvarchar(50)", "clrTypeText": "string", "isNullable": true }
              ],
              "parameters": [
                { "name": "id", "sqlTypeName": "int", "clrTypeText": "int" }
              ]
            }

            """;

        var serialized = SnapshotWriter.Serialize(
            "SELECT Id, Name FROM dbo.Items WHERE Id = @id", Description);

        Assert.Equal(expected.Replace("\r\n", "\n"), serialized);
    }

    [Fact]
    public void Should_SerializeEmptyArrays_When_StatementHasNoColumnsOrParameters()
    {
        var serialized = SnapshotWriter.Serialize("DELETE FROM dbo.Items", new QueryDescription([], []));

        Assert.Contains("\"columns\": []", serialized);
        Assert.Contains("\"parameters\": []", serialized);
    }

    [Fact]
    public void Should_RoundTripThroughAnalyzerReader_When_SqlNeedsEscaping()
    {
        const string commandText = "SELECT '\"' AS Quote,\n\t1 AS N FROM dbo.Items WHERE Name = 'O''Brien \\ co'";
        var serialized = SnapshotWriter.Serialize(commandText, Description);

        var read = Generators.QuerySnapshotReader.TryRead(serialized, out var snapshot);

        Assert.True(read);
        Assert.Equal(commandText, snapshot!.CommandText);
        Assert.Equal("sqlserver", snapshot.Provider);
        Assert.Equal(
            Description.Columns.Select(c => (c.Ordinal, c.Name, c.SqlTypeName, c.ClrTypeText, c.IsNullable)),
            snapshot.Columns.Select(c => (c.Ordinal, c.Name, c.SqlTypeName, c.ClrTypeText, c.IsNullable)));
        Assert.Equal(
            Description.Parameters.Select(p => (p.Name, p.SqlTypeName, p.ClrTypeText)),
            snapshot.Parameters.Select(p => (p.Name, p.SqlTypeName, p.ClrTypeText)));
    }

    [Fact]
    public void Should_SerializeNullClrTypeText_When_ParameterTypeIsUnknown()
    {
        var description = new QueryDescription([], [new DescribedParameter("id", string.Empty, ClrTypeText: null)]);

        var serialized = SnapshotWriter.Serialize("SELECT id FROM items WHERE id = @id", description);

        Assert.Contains("\"clrTypeText\": null }", serialized);

        var read = Generators.QuerySnapshotReader.TryRead(serialized, out var snapshot);
        Assert.True(read);
        var parameter = Assert.Single(snapshot!.Parameters);
        Assert.Null(parameter.ClrTypeText);
    }

    [Fact]
    public void Should_NameFileWithAnalyzerKey_When_PairingSnapshotWithQuery()
    {
        const string commandText = "SELECT Id FROM dbo.Items";

        var fileName = SnapshotWriter.FileName(commandText);

        // The writer's key must match the analyzer's, or prepare and verification silently split.
        Assert.Equal($"query-{Generators.SnapshotKey.Compute(commandText)}.json", fileName);
        Assert.Matches("^query-[0-9a-f]{64}\\.json$", fileName);
    }
}
