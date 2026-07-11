using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace SqlBound.UnitTests.Fakes;

internal sealed class FakeDbParameter : DbParameter
{
    public override DbType DbType { get; set; }

    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

    public override bool IsNullable { get; set; }

    [AllowNull]
    public override string ParameterName { get; set; } = string.Empty;

    public override int Size { get; set; }

    [AllowNull]
    public override string SourceColumn { get; set; } = string.Empty;

    public override bool SourceColumnNullMapping { get; set; }

    public override DataRowVersion SourceVersion { get; set; }

    public override object? Value { get; set; }

    public override void ResetDbType() => DbType = default;
}
