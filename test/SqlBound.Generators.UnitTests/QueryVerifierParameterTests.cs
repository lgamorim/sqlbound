namespace SqlBound.Generators.UnitTests;

public sealed class QueryVerifierParameterTests
{
    [Fact]
    public void Should_ReportNothing_When_ParametersMatchByNameAndType()
    {
        var model = QueryModel(
            new MethodParameterModel("id", "int", ParameterKind.Scalar),
            new MethodParameterModel("name", "string?", ParameterKind.Scalar, CanBeNull: true));
        var snapshot = Snapshot(
            new SnapshotParameter("id", "int", "int"),
            new SnapshotParameter("name", "nvarchar(50)", "string"));

        Assert.Empty(QueryVerifier.Verify(model, snapshot));
    }

    [Fact]
    public void Should_ReportNothing_When_ParameterNamesDifferOnlyByCase()
    {
        var model = QueryModel(new MethodParameterModel("Id", "int", ParameterKind.Scalar));
        var snapshot = Snapshot(new SnapshotParameter("id", "int", "int"));

        Assert.Empty(QueryVerifier.Verify(model, snapshot));
    }

    [Fact]
    public void Should_IgnoreInfrastructureParameters_When_MatchingAgainstSqlParameters()
    {
        var model = QueryModel(
            new MethodParameterModel("connection", "global::System.Data.Common.DbConnection", ParameterKind.Connection),
            new MethodParameterModel("transaction", "global::System.Data.Common.DbTransaction", ParameterKind.Transaction),
            new MethodParameterModel("id", "int", ParameterKind.Scalar),
            new MethodParameterModel("cancellationToken", "global::System.Threading.CancellationToken", ParameterKind.CancellationToken));
        var snapshot = Snapshot(new SnapshotParameter("id", "int", "int"));

        Assert.Empty(QueryVerifier.Verify(model, snapshot));
    }

    [Fact]
    public void Should_ReportMissingParameter_When_SqlUsesPlaceholderTheMethodLacks()
    {
        var model = QueryModel(new MethodParameterModel("id", "int", ParameterKind.Scalar));
        var snapshot = Snapshot(
            new SnapshotParameter("id", "int", "int"),
            new SnapshotParameter("category", "nvarchar(20)", "string"));

        var finding = Assert.Single(QueryVerifier.Verify(model, snapshot));
        Assert.Equal("SQLB108", finding.Descriptor.Id);
        Assert.Contains("category", finding.MessageArgs);
    }

    [Fact]
    public void Should_ReportUnusedParameter_When_MethodDeclaresScalarTheSqlNeverUses()
    {
        var model = QueryModel(
            new MethodParameterModel("id", "int", ParameterKind.Scalar),
            new MethodParameterModel("unused", "int", ParameterKind.Scalar));
        var snapshot = Snapshot(new SnapshotParameter("id", "int", "int"));

        var finding = Assert.Single(QueryVerifier.Verify(model, snapshot));
        Assert.Equal("SQLB109", finding.Descriptor.Id);
        Assert.Contains("unused", finding.MessageArgs);
    }

    [Fact]
    public void Should_ReportParameterTypeMismatch_When_DeclaredTypeDiffers()
    {
        var model = QueryModel(new MethodParameterModel("id", "string", ParameterKind.Scalar));
        var snapshot = Snapshot(new SnapshotParameter("id", "int", "int"));

        var finding = Assert.Single(QueryVerifier.Verify(model, snapshot));
        Assert.Equal("SQLB110", finding.Descriptor.Id);
        Assert.Contains("string", finding.MessageArgs);
        Assert.Contains("int", finding.MessageArgs);
    }

    [Fact]
    public void Should_ReportNothing_When_ParameterClrTypeIsUnknown()
    {
        // SQLite has no static parameter typing: a snapshot with a null ClrTypeText means the
        // provider could not infer one, so SQLB110 has nothing to compare against and must stay
        // silent even though the declared C# type looks unrelated to the (empty) SQL type name.
        var model = QueryModel(new MethodParameterModel("id", "string", ParameterKind.Scalar));
        var snapshot = Snapshot(new SnapshotParameter("id", string.Empty, ClrTypeText: null));

        Assert.Empty(QueryVerifier.Verify(model, snapshot));
    }

    [Fact]
    public void Should_ReportResultSetOnExecute_When_ExecuteStatementReturnsColumns()
    {
        var model = ExecuteModel();
        var snapshot = new QuerySnapshot(
            "DELETE FROM dbo.Items OUTPUT DELETED.Id",
            "sqlserver",
            new EquatableArray<SnapshotColumn>([new SnapshotColumn(0, "Id", "int", "int", IsNullable: false)]),
            EquatableArray<SnapshotParameter>.Empty);

        var finding = Assert.Single(QueryVerifier.Verify(model, snapshot));
        Assert.Equal("SQLB111", finding.Descriptor.Id);
    }

    [Fact]
    public void Should_ReportNothing_When_ExecuteStatementReturnsNoColumns()
    {
        var snapshot = new QuerySnapshot(
            "DELETE FROM dbo.Items",
            "sqlserver",
            EquatableArray<SnapshotColumn>.Empty,
            EquatableArray<SnapshotParameter>.Empty);

        Assert.Empty(QueryVerifier.Verify(ExecuteModel(), snapshot));
    }

    private static QueryMethodModel QueryModel(params MethodParameterModel[] parameters) =>
        Model(ResultShape.SingleRow, ResultElementKind.Scalar,
            [new ColumnModel(string.Empty, "int", "GetInt32", IsNullable: false)], parameters);

    private static QueryMethodModel ExecuteModel(params MethodParameterModel[] parameters) =>
        Model(ResultShape.Execute, ResultElementKind.Row, [], parameters);

    private static QueryMethodModel Model(
        ResultShape shape, ResultElementKind elementKind, ColumnModel[] columns, MethodParameterModel[] parameters) =>
        new(
            "App",
            new EquatableArray<ContainingTypeModel>([new ContainingTypeModel("class", "ItemQueries")]),
            "public",
            "GetItemAsync",
            IsExtensionMethod: false,
            "SELECT COUNT(*) FROM dbo.Items",
            "global::System.Threading.Tasks.Task<int>",
            shape,
            elementKind,
            RowMappingKind.Constructor,
            "int",
            new EquatableArray<ColumnModel>(columns),
            new EquatableArray<MethodParameterModel>(parameters));

    private static QuerySnapshot Snapshot(params SnapshotParameter[] parameters) =>
        new(
            "SELECT COUNT(*) FROM dbo.Items",
            "sqlserver",
            new EquatableArray<SnapshotColumn>([new SnapshotColumn(0, "", "int", "int", IsNullable: false)]),
            new EquatableArray<SnapshotParameter>(parameters));
}
