namespace SqlBound;

/// <summary>
/// Marks a <c>static partial</c> method whose implementation is emitted by the SqlBound source
/// generator: the method executes <paramref name="commandText"/> and materializes the result
/// rows into the method's declared return type with straight-line, reflection-free reader code.
/// </summary>
/// <param name="commandText">The SQL statement the generated implementation executes.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SqlQueryAttribute(string commandText) : Attribute
{
    /// <summary>Gets the SQL statement the generated implementation executes.</summary>
    public string CommandText { get; } = commandText ?? throw new ArgumentNullException(nameof(commandText));
}
