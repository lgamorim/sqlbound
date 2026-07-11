using System.Data;
using System.Data.Common;

namespace SqlBound;

/// <summary>
/// Executes SQL against an already-open <see cref="DbConnection"/>. SqlSession never opens,
/// closes, or otherwise owns the connection or its lifecycle — that responsibility always
/// stays with the caller, so the same connection (and transaction) can be shared safely with
/// other data-access libraries.
/// </summary>
public sealed class SqlSession
{
    private readonly DbConnection _connection;
    private readonly DbTransaction? _transaction;

    /// <summary>
    /// Initializes a new session over the given connection and, optionally, an existing
    /// transaction to enlist every command in.
    /// </summary>
    public SqlSession(DbConnection connection, DbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
        _transaction = transaction;
    }

    /// <summary>
    /// Executes a non-query statement and returns the number of affected rows.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="sql"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">The connection is not open.</exception>
    public async Task<int> RunAsync(
        string sql,
        SqlParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ValidateAndPrepare(sql, cancellationToken);

        using var command = CreateCommand(sql, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a statement and returns its first column, first row as <typeparamref name="T"/>.
    /// A database null converts to <see langword="default"/> when <typeparamref name="T"/> can
    /// represent it (a reference type or <see cref="Nullable{T}"/>); otherwise it throws.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="sql"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">
    /// The connection is not open, or the result is a database null and <typeparamref name="T"/>
    /// is a non-nullable value type.
    /// </exception>
    public async Task<T?> FetchScalarAsync<T>(
        string sql,
        SqlParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ValidateAndPrepare(sql, cancellationToken);

        using var command = CreateCommand(sql, parameters);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return ConvertScalar<T>(result);
    }

    private static T? ConvertScalar<T>(object? result)
    {
        if (result is null or DBNull)
        {
            if (default(T) is not null)
            {
                throw new InvalidOperationException(
                    $"The query returned no value, but '{typeof(T)}' is a non-nullable value type.");
            }

            return default;
        }

        if (result is T typed)
        {
            return typed;
        }

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(result, targetType);
    }

    private void ValidateAndPrepare(string sql, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnectionOpen();
    }

    private void EnsureConnectionOpen()
    {
        if (_connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException(
                $"The connection must be open before executing a command. Current state: {_connection.State}.");
        }
    }

    private DbCommand CreateCommand(string sql, SqlParameters? parameters)
    {
        var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = _transaction;

        if (parameters is not null)
        {
            foreach (var (name, value) in parameters.Items)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }
        }

        return command;
    }
}
