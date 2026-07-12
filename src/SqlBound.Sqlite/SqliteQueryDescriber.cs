using System.Data.Common;
using Microsoft.Data.Sqlite;
using SQLitePCL;
using SqlBound.Introspection;

namespace SqlBound.Sqlite;

/// <summary>
/// Describes a command text against a live SQLite database via <c>sqlite3_prepare_v2</c> and its
/// companion metadata calls. Per ADR 0001 this belongs exclusively to the CLI <c>prepare</c> step
/// (or the opt-in MSBuild task) — it must never run inside the Roslyn analyzer or at application
/// runtime. Unlike SQL Server's <c>sp_describe_first_result_set</c>, SQLite can only report a
/// declared type for a direct table column reference (<c>sqlite3_column_decltype</c> returns
/// <see langword="null"/> for any computed expression, function call, or aggregate); describing
/// such a column is a documented limitation, not a bug — see docs/introspection.md.
/// </summary>
public sealed class SqliteQueryDescriber : IQueryDescriber
{
    /// <summary>Describes <paramref name="commandText"/>'s result columns and parameters.</summary>
    /// <param name="connection">An open <see cref="SqliteConnection"/> to the database to describe against.</param>
    /// <param name="commandText">The command text to describe.</param>
    /// <param name="cancellationToken">Cancels the describe round-trip.</param>
    /// <exception cref="ArgumentException"><paramref name="connection"/> is not a <see cref="SqliteConnection"/>.</exception>
    /// <exception cref="SqlBoundDescribeException">SQLite could not describe the command, a column has no declared type, or a described type has no SqlBound-supported mapping.</exception>
    public Task<QueryDescription> DescribeAsync(
        DbConnection connection, string commandText, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);
        if (connection is not SqliteConnection sqliteConnection)
        {
            throw new ArgumentException(
                $"SqliteQueryDescriber requires a {typeof(SqliteConnection)}, but received a {connection.GetType()}.",
                nameof(connection));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var handle = sqliteConnection.Handle
            ?? throw new InvalidOperationException("The connection must be open before it can be described against.");
        var prepareResult = raw.sqlite3_prepare_v2(handle, commandText, out var statement);
        if (prepareResult != raw.SQLITE_OK)
        {
            throw new SqlBoundDescribeException(
                $"SQLite could not describe the command: {raw.sqlite3_errmsg(handle).utf8_to_string()}", commandText);
        }

        try
        {
            var columns = DescribeColumns(handle, statement, commandText);
            var parameters = DescribeParameters(statement, commandText);
            return Task.FromResult(new QueryDescription(columns, parameters));
        }
        finally
        {
            raw.sqlite3_finalize(statement);
        }
    }

    private static IReadOnlyList<DescribedColumn> DescribeColumns(sqlite3 handle, sqlite3_stmt statement, string commandText)
    {
        var columnCount = raw.sqlite3_column_count(statement);
        var columns = new List<DescribedColumn>(columnCount);
        for (var ordinal = 0; ordinal < columnCount; ordinal++)
        {
            var name = raw.sqlite3_column_name(statement, ordinal).utf8_to_string() ?? string.Empty;
            var declaredType = raw.sqlite3_column_decltype(statement, ordinal).utf8_to_string();
            if (string.IsNullOrEmpty(declaredType))
            {
                throw new SqlBoundDescribeException(
                    $"Column '{name}' has no declared type. SqlBound can only describe direct table column " +
                    "references for SQLite; computed expressions, function calls, and aggregates " +
                    "(e.g. COUNT(*), CAST, arithmetic) have no declared type and cannot be described.",
                    commandText);
            }

            if (!SqliteTypeMap.TryMap(declaredType, out var clrTypeText))
            {
                throw new SqlBoundDescribeException(
                    $"Column '{name}' has declared type '{declaredType}', which SqlBound cannot materialize.",
                    commandText);
            }

            columns.Add(new DescribedColumn(ordinal, name, declaredType, clrTypeText, IsNullable(handle, statement, ordinal)));
        }

        return columns;
    }

    private static bool IsNullable(sqlite3 handle, sqlite3_stmt statement, int ordinal)
    {
        var tableName = raw.sqlite3_column_table_name(statement, ordinal).utf8_to_string();
        var originName = raw.sqlite3_column_origin_name(statement, ordinal).utf8_to_string();
        if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(originName))
        {
            // A declared type only exists for a direct table column reference, so this shouldn't
            // happen for a column that reached this point - default to nullable (the safe
            // direction) if the origin can't be resolved.
            return true;
        }

        var metadataResult = raw.sqlite3_table_column_metadata(
            handle, "main", tableName, originName, out _, out _, out var notNull, out _, out _);
        return metadataResult != raw.SQLITE_OK || notNull == 0;
    }

    private static IReadOnlyList<DescribedParameter> DescribeParameters(sqlite3_stmt statement, string commandText)
    {
        var parameterCount = raw.sqlite3_bind_parameter_count(statement);
        var parameters = new List<DescribedParameter>(parameterCount);
        for (var index = 1; index <= parameterCount; index++)
        {
            var rawName = raw.sqlite3_bind_parameter_name(statement, index).utf8_to_string();
            if (string.IsNullOrEmpty(rawName))
            {
                throw new SqlBoundDescribeException(
                    $"Parameter {index} is positional and has no name. SqlBound requires named parameters " +
                    "(e.g. @id) so each one can bind to a C# method parameter by name.",
                    commandText);
            }

            // SQLite has no static parameter typing (no equivalent of SQL Server's
            // sp_describe_undeclared_parameters), so only the name - stripped of its marker
            // character - can be reported; SqlTypeName and ClrTypeText stay empty/null.
            parameters.Add(new DescribedParameter(rawName.Substring(1), string.Empty, ClrTypeText: null));
        }

        return parameters;
    }
}
