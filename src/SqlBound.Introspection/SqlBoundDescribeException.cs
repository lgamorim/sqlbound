namespace SqlBound.Introspection;

/// <summary>
/// Thrown when a provider cannot describe a command text, or describes it with a type SqlBound
/// cannot materialize. Carries the offending command text so <c>prepare</c>-step tooling can
/// point at the query that failed.
/// </summary>
public sealed class SqlBoundDescribeException : Exception
{
    /// <summary>Initializes the exception for a describe failure detected by SqlBound itself.</summary>
    /// <param name="message">The failure description.</param>
    /// <param name="commandText">The command text that failed to describe.</param>
    public SqlBoundDescribeException(string message, string commandText)
        : base(message)
    {
        CommandText = commandText;
    }

    /// <summary>Initializes the exception for a describe failure reported by the provider.</summary>
    /// <param name="message">The failure description.</param>
    /// <param name="commandText">The command text that failed to describe.</param>
    /// <param name="innerException">The provider exception raised.</param>
    public SqlBoundDescribeException(string message, string commandText, Exception innerException)
        : base(message, innerException)
    {
        CommandText = commandText;
    }

    /// <summary>The command text that failed to describe.</summary>
    public string CommandText { get; }
}
