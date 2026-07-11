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
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureConnectionOpen();

        using var command = CreateCommand(sql, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
