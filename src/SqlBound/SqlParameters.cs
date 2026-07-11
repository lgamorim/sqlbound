namespace SqlBound;

/// <summary>
/// An immutable, named set of SQL parameter values. Null values are normalized to
/// <see cref="DBNull"/> so callers can bind them to an ADO.NET parameter without a
/// separate null check.
/// </summary>
public sealed class SqlParameters
{
    private readonly KeyValuePair<string, object>[] _items;

    /// <summary>
    /// Initializes a new instance with the given named parameter values. Names must be
    /// non-empty and unique.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A parameter name is null, empty, whitespace, or duplicated.
    /// </exception>
    public SqlParameters(params (string Name, object? Value)[] parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var items = new KeyValuePair<string, object>[parameters.Length];
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < parameters.Length; i++)
        {
            var (name, value) = parameters[i];

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Parameter name must not be null or whitespace.", nameof(parameters));
            }

            if (!seenNames.Add(name))
            {
                throw new ArgumentException($"Duplicate parameter name '{name}'.", nameof(parameters));
            }

            items[i] = new KeyValuePair<string, object>(name, value ?? DBNull.Value);
        }

        _items = items;
    }

    /// <summary>
    /// An empty parameter set.
    /// </summary>
    public static SqlParameters Empty { get; } = new();

    /// <summary>
    /// The number of parameters in this set.
    /// </summary>
    public int Count => _items.Length;

    /// <summary>
    /// The parameters, in the order they were given. Values are never null; a null
    /// value provided at construction is normalized to <see cref="DBNull.Value"/>.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, object>> Items => _items;
}
