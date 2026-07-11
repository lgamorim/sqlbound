using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SqlBound.Generators;

/// <summary>
/// A value-equatable stand-in for <see cref="Diagnostic"/> so diagnostics can travel through the
/// incremental pipeline without breaking model caching (<see cref="Diagnostic"/> itself holds
/// non-equatable state such as <see cref="Location"/>).
/// </summary>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo Location,
    EquatableArray<string> MessageArgs)
{
    public Diagnostic CreateDiagnostic() =>
        Diagnostic.Create(Descriptor, Location.ToLocation(), MessageArgs.Cast<object>().ToArray());
}

/// <summary>The value-equatable pieces of a <see cref="Location"/>, for <see cref="DiagnosticInfo"/>.</summary>
internal sealed record LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    public static LocationInfo From(Location location) => new(
        location.SourceTree?.FilePath ?? string.Empty,
        location.SourceSpan,
        location.GetLineSpan().Span);
}
