namespace SqlBound;

/// <summary>
/// Marks a <c>static partial</c> method whose implementation is emitted by the SqlBound source
/// generator: the method executes <paramref name="commandText"/> as a non-query statement
/// (INSERT/UPDATE/DELETE/DDL), returning the number of affected rows (<c>Task&lt;int&gt;</c>)
/// or discarding it (<c>Task</c>).
/// </summary>
/// <param name="commandText">The SQL statement the generated implementation executes.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SqlExecuteAttribute(string commandText) : Attribute
{
    /// <summary>Gets the SQL statement the generated implementation executes.</summary>
    public string CommandText { get; } = commandText ?? throw new ArgumentNullException(nameof(commandText));
}
