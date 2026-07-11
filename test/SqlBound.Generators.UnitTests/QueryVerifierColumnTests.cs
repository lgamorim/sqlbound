namespace SqlBound.Generators.UnitTests;

public sealed class QueryVerifierColumnTests
{
    [Fact]
    public void Should_ReportNothing_When_ColumnsMatchByNameTypeAndNullability()
    {
        var model = RowModel(
            new ColumnModel("Id", "int", "GetInt32", IsNullable: false),
            new ColumnModel("Price", "decimal?", "GetDecimal", IsNullable: true));
        var snapshot = Snapshot(
            new SnapshotColumn(0, "Id", "int", "int", IsNullable: false),
            new SnapshotColumn(1, "Price", "decimal(18,2)", "decimal", IsNullable: true));

        Assert.Empty(QueryVerifier.Verify(model, snapshot));
    }

    [Fact]
    public void Should_ReportNothing_When_ColumnNamesDifferOnlyByCase()
    {
        var model = RowModel(new ColumnModel("id", "int", "GetInt32", IsNullable: false));
        var snapshot = Snapshot(new SnapshotColumn(0, "ID", "int", "int", IsNullable: false));

        Assert.Empty(QueryVerifier.Verify(model, snapshot));
    }

    [Fact]
    public void Should_ReportNothing_When_DeclarationIsNullableOverNonNullableColumn()
    {
        // Over-nullable declarations are safe: the generated code just never sees a DBNull.
        var model = RowModel(new ColumnModel("Name", "string?", "GetString", IsNullable: true));
        var snapshot = Snapshot(new SnapshotColumn(0, "Name", "nvarchar(50)", "string", IsNullable: false));

        Assert.Empty(QueryVerifier.Verify(model, snapshot));
    }

    [Fact]
    public void Should_ReportNoResultSet_When_QueryShapeDescribesZeroColumns()
    {
        var model = RowModel(new ColumnModel("Id", "int", "GetInt32", IsNullable: false));
        var snapshot = Snapshot();

        var finding = Assert.Single(QueryVerifier.Verify(model, snapshot));
        Assert.Equal("SQLB103", finding.Descriptor.Id);
    }

    [Fact]
    public void Should_ReportMissingColumn_When_DeclaredNameIsAbsentFromResultSet()
    {
        var model = RowModel(
            new ColumnModel("Id", "int", "GetInt32", IsNullable: false),
            new ColumnModel("Label", "string", "GetString", IsNullable: false));
        var snapshot = Snapshot(
            new SnapshotColumn(0, "Id", "int", "int", IsNullable: false),
            new SnapshotColumn(1, "Name", "nvarchar(50)", "string", IsNullable: false));

        var findings = QueryVerifier.Verify(model, snapshot);

        Assert.Contains(findings, f => f.Descriptor.Id == "SQLB104" && f.MessageArgs.Contains("Label"));
    }

    [Fact]
    public void Should_ReportTypeMismatch_When_DeclaredColumnTypeDiffers()
    {
        var model = RowModel(new ColumnModel("Id", "long", "GetInt64", IsNullable: false));
        var snapshot = Snapshot(new SnapshotColumn(0, "Id", "int", "int", IsNullable: false));

        var finding = Assert.Single(QueryVerifier.Verify(model, snapshot));
        Assert.Equal("SQLB105", finding.Descriptor.Id);
        Assert.Contains("long", finding.MessageArgs);
        Assert.Contains("int", finding.MessageArgs);
    }

    [Fact]
    public void Should_ReportNullabilityMismatch_When_DatabaseColumnIsNullableButDeclarationIsNot()
    {
        var model = RowModel(new ColumnModel("Price", "decimal", "GetDecimal", IsNullable: false));
        var snapshot = Snapshot(new SnapshotColumn(0, "Price", "decimal(18,2)", "decimal", IsNullable: true));

        var finding = Assert.Single(QueryVerifier.Verify(model, snapshot));
        Assert.Equal("SQLB106", finding.Descriptor.Id);
    }

    [Fact]
    public void Should_ReportUnreadColumns_When_ResultSetReturnsMoreThanTheMethodReads()
    {
        var model = RowModel(new ColumnModel("Id", "int", "GetInt32", IsNullable: false));
        var snapshot = Snapshot(
            new SnapshotColumn(0, "Id", "int", "int", IsNullable: false),
            new SnapshotColumn(1, "Name", "nvarchar(50)", "string", IsNullable: false),
            new SnapshotColumn(2, "Price", "decimal(18,2)", "decimal", IsNullable: true));

        var finding = Assert.Single(QueryVerifier.Verify(model, snapshot));
        Assert.Equal("SQLB107", finding.Descriptor.Id);
        Assert.Contains("Name, Price", finding.MessageArgs);
    }

    [Fact]
    public void Should_VerifyFirstColumnOnly_When_ShapeIsScalar()
    {
        var model = ScalarModel(new ColumnModel(string.Empty, "int", "GetInt32", IsNullable: false));
        var snapshot = Snapshot(new SnapshotColumn(0, "", "bigint", "long", IsNullable: false));

        var finding = Assert.Single(QueryVerifier.Verify(model, snapshot));
        Assert.Equal("SQLB105", finding.Descriptor.Id);
    }

    [Fact]
    public void Should_ReportUnreadColumns_When_ScalarQueryReturnsSeveralColumns()
    {
        var model = ScalarModel(new ColumnModel(string.Empty, "int", "GetInt32", IsNullable: false));
        var snapshot = Snapshot(
            new SnapshotColumn(0, "Total", "int", "int", IsNullable: false),
            new SnapshotColumn(1, "Name", "nvarchar(50)", "string", IsNullable: false));

        var finding = Assert.Single(QueryVerifier.Verify(model, snapshot));
        Assert.Equal("SQLB107", finding.Descriptor.Id);
    }

    [Fact]
    public void Should_ReportNullabilityMismatch_When_ScalarColumnIsNullableButDeclarationIsNot()
    {
        var model = ScalarModel(new ColumnModel(string.Empty, "decimal", "GetDecimal", IsNullable: false));
        var snapshot = Snapshot(new SnapshotColumn(0, "", "decimal(18,2)", "decimal", IsNullable: true));

        var finding = Assert.Single(QueryVerifier.Verify(model, snapshot));
        Assert.Equal("SQLB106", finding.Descriptor.Id);
    }

    private static QueryMethodModel RowModel(params ColumnModel[] columns) =>
        Model(ResultShape.RowList, ResultElementKind.Row, columns);

    private static QueryMethodModel ScalarModel(params ColumnModel[] columns) =>
        Model(ResultShape.SingleRow, ResultElementKind.Scalar, columns);

    private static QueryMethodModel Model(ResultShape shape, ResultElementKind elementKind, ColumnModel[] columns) =>
        new(
            "App",
            new EquatableArray<ContainingTypeModel>([new ContainingTypeModel("class", "ItemQueries")]),
            "public",
            "GetItemsAsync",
            IsExtensionMethod: false,
            "SELECT Id FROM dbo.Items",
            "global::System.Threading.Tasks.Task<int>",
            shape,
            elementKind,
            RowMappingKind.Constructor,
            "global::App.Item",
            new EquatableArray<ColumnModel>(columns),
            EquatableArray<MethodParameterModel>.Empty);

    private static QuerySnapshot Snapshot(params SnapshotColumn[] columns) =>
        new(
            "SELECT Id FROM dbo.Items",
            "sqlserver",
            new EquatableArray<SnapshotColumn>(columns),
            EquatableArray<SnapshotParameter>.Empty);
}
