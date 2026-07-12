namespace SqlBound.Generators.UnitTests;

public sealed class QuerySnapshotReaderTests
{
    private const string ValidSnapshot =
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

    [Fact]
    public void Should_ReadCommandTextColumnsAndParameters_When_SnapshotIsValid()
    {
        var read = QuerySnapshotReader.TryRead(ValidSnapshot, out var snapshot);

        Assert.True(read);
        Assert.NotNull(snapshot);
        Assert.Equal("SELECT Id, Name FROM dbo.Items WHERE Id = @id", snapshot.CommandText);
        Assert.Equal("sqlserver", snapshot.Provider);
        Assert.Equal(
            [
                new SnapshotColumn(0, "Id", "int", "int", IsNullable: false),
                new SnapshotColumn(1, "Name", "nvarchar(50)", "string", IsNullable: true),
            ],
            snapshot.Columns);
        Assert.Equal(
            [new SnapshotParameter("id", "int", "int")],
            snapshot.Parameters);
    }

    [Fact]
    public void Should_ReadEmptyCollections_When_QueryHasNoColumnsOrParameters()
    {
        var read = QuerySnapshotReader.TryRead(
            """{ "commandText": "DELETE FROM dbo.Items", "provider": "sqlserver", "columns": [], "parameters": [] }""",
            out var snapshot);

        Assert.True(read);
        Assert.NotNull(snapshot);
        Assert.Empty(snapshot.Columns);
        Assert.Empty(snapshot.Parameters);
    }

    [Fact]
    public void Should_UnescapeStrings_When_SnapshotUsesJsonEscapes()
    {
        var read = QuerySnapshotReader.TryRead(
            """{ "commandText": "SELECT '\"' AS Q,\n1 AS N", "provider": "sqlserver", "columns": [], "parameters": [] }""",
            out var snapshot);

        Assert.True(read);
        Assert.Equal("SELECT '\"' AS Q,\n1 AS N", snapshot!.CommandText);
    }

    [Fact]
    public void Should_ReadNullClrTypeText_When_ProviderCannotInferAParameterType()
    {
        var read = QuerySnapshotReader.TryRead(
            """
            {
              "commandText": "SELECT id FROM items WHERE id = @id",
              "provider": "sqlite",
              "columns": [],
              "parameters": [ { "name": "id", "sqlTypeName": "", "clrTypeText": null } ]
            }
            """,
            out var snapshot);

        Assert.True(read);
        var parameter = Assert.Single(snapshot!.Parameters);
        Assert.Equal("id", parameter.Name);
        Assert.Null(parameter.ClrTypeText);
    }

    [Fact]
    public void Should_IgnoreUnknownFields_When_SnapshotCarriesExtraMetadata()
    {
        var read = QuerySnapshotReader.TryRead(
            """
            {
              "commandText": "SELECT 1", "provider": "sqlserver",
              "describedAtUtc": "2026-07-11T00:00:00Z", "schemaVersion": 2,
              "columns": [ { "ordinal": 0, "name": "", "sqlTypeName": "int", "clrTypeText": "int", "isNullable": false, "extra": null } ],
              "parameters": []
            }
            """,
            out var snapshot);

        Assert.True(read);
        Assert.Equal("", snapshot!.Columns[0].Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{")]
    [InlineData("""{ "commandText": "SELECT 1" }""")]
    [InlineData("""{ "commandText": "SELECT 1", "provider": "sqlserver", "columns": [], "parameters": [] } trailing""")]
    [InlineData("""{ "commandText": 42, "provider": "sqlserver", "columns": [], "parameters": [] }""")]
    [InlineData("""{ "commandText": "SELECT 1", "provider": "sqlserver", "columns": {}, "parameters": [] }""")]
    [InlineData("""{ "commandText": "SELECT 1", "provider": "sqlserver", "columns": [ { "ordinal": 0 } ], "parameters": [] }""")]
    [InlineData("""{ "commandText": "SELECT 1", "provider": "sqlserver", "columns": [ { "ordinal": 0.5, "name": "", "sqlTypeName": "int", "clrTypeText": "int", "isNullable": false } ], "parameters": [] }""")]
    [InlineData("""{ "commandText": "SELECT 1", "provider": "sqlserver", "columns": [], "parameters": [ { "name": "id" } ] }""")]
    public void Should_ReturnFalse_When_SnapshotIsMalformedOrIncomplete(string text)
    {
        var read = QuerySnapshotReader.TryRead(text, out var snapshot);

        Assert.False(read);
        Assert.Null(snapshot);
    }
}
