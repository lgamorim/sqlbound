using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using SqlBound.Introspection;

namespace SqlBound.SqlServer;

/// <summary>
/// Describes a command text against a live SQL Server using <c>sp_describe_first_result_set</c>.
/// Per ADR 0001 this round-trip belongs exclusively to the CLI <c>prepare</c> step (or the opt-in
/// MSBuild task) — it must never run inside the Roslyn analyzer or at application runtime.
/// </summary>
public sealed class SqlServerQueryDescriber : IQueryDescriber
{
    /// <summary>Describes <paramref name="commandText"/>'s result columns and parameters.</summary>
    /// <param name="connection">An open <see cref="SqlConnection"/> to the database to describe against.</param>
    /// <param name="commandText">The command text to describe.</param>
    /// <param name="cancellationToken">Cancels the describe round-trips.</param>
    /// <exception cref="ArgumentException"><paramref name="connection"/> is not a <see cref="SqlConnection"/>.</exception>
    /// <exception cref="SqlBoundDescribeException">SQL Server could not describe the command, or a described type has no SqlBound-supported mapping.</exception>
    public async Task<QueryDescription> DescribeAsync(
        DbConnection connection, string commandText, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);
        if (connection is not SqlConnection sqlConnection)
        {
            throw new ArgumentException(
                $"SqlServerQueryDescriber requires a {typeof(SqlConnection)}, but received a {connection.GetType()}.",
                nameof(connection));
        }

        try
        {
            var columns = await DescribeColumnsAsync(sqlConnection, commandText, cancellationToken).ConfigureAwait(false);
            var parameters = await DescribeParametersAsync(sqlConnection, commandText, cancellationToken).ConfigureAwait(false);
            return new QueryDescription(columns, parameters);
        }
        catch (SqlException exception)
        {
            throw new SqlBoundDescribeException(
                $"SQL Server could not describe the command: {exception.Message}", commandText, exception);
        }
    }

    private static async Task<IReadOnlyList<DescribedColumn>> DescribeColumnsAsync(
        SqlConnection connection, string commandText, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.CommandText = "sys.sp_describe_first_result_set";
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@tsql", SqlDbType.NVarChar, -1) { Value = commandText });

            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                // A statement with no result set (e.g. a bare DELETE) describes as zero rows.
                if (reader.FieldCount == 0)
                {
                    return [];
                }

                var isHiddenOrdinal = reader.GetOrdinal("is_hidden");
                var columnOrdinalOrdinal = reader.GetOrdinal("column_ordinal");
                var nameOrdinal = reader.GetOrdinal("name");
                var isNullableOrdinal = reader.GetOrdinal("is_nullable");
                var systemTypeNameOrdinal = reader.GetOrdinal("system_type_name");
                var userTypeNameOrdinal = reader.GetOrdinal("user_type_name");

                var columns = new List<DescribedColumn>();
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    // Hidden columns only appear in browse mode, which this call never enables;
                    // the guard keeps a contract change from leaking phantom columns.
                    if (reader.GetBoolean(isHiddenOrdinal))
                    {
                        continue;
                    }

                    var name = reader.IsDBNull(nameOrdinal) ? string.Empty : reader.GetString(nameOrdinal);
                    var sqlTypeName = ReadTypeName(reader, systemTypeNameOrdinal, userTypeNameOrdinal);
                    if (!SqlServerTypeMap.TryMap(sqlTypeName, out var clrTypeText))
                    {
                        throw new SqlBoundDescribeException(
                            $"Result column '{name}' has SQL type '{sqlTypeName}', which SqlBound cannot materialize.",
                            commandText);
                    }

                    columns.Add(new DescribedColumn(
                        reader.GetInt32(columnOrdinalOrdinal) - 1,
                        name,
                        sqlTypeName,
                        clrTypeText,
                        reader.GetBoolean(isNullableOrdinal)));
                }

                return columns;
            }
        }
    }

    private static async Task<IReadOnlyList<DescribedParameter>> DescribeParametersAsync(
        SqlConnection connection, string commandText, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.CommandText = "sys.sp_describe_undeclared_parameters";
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@tsql", SqlDbType.NVarChar, -1) { Value = commandText });

            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                if (reader.FieldCount == 0)
                {
                    return [];
                }

                var nameOrdinal = reader.GetOrdinal("name");
                var systemTypeNameOrdinal = reader.GetOrdinal("suggested_system_type_name");
                var userTypeNameOrdinal = reader.GetOrdinal("suggested_user_type_name");

                var parameters = new List<DescribedParameter>();
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var name = reader.GetString(nameOrdinal).TrimStart('@');
                    var sqlTypeName = ReadTypeName(reader, systemTypeNameOrdinal, userTypeNameOrdinal);
                    if (!SqlServerTypeMap.TryMap(sqlTypeName, out var clrTypeText))
                    {
                        throw new SqlBoundDescribeException(
                            $"Parameter '@{name}' has suggested SQL type '{sqlTypeName}', which SqlBound cannot bind.",
                            commandText);
                    }

                    parameters.Add(new DescribedParameter(name, sqlTypeName, clrTypeText));
                }

                return parameters;
            }
        }
    }

    private static string ReadTypeName(DbDataReader reader, int systemTypeNameOrdinal, int userTypeNameOrdinal)
    {
        if (!reader.IsDBNull(systemTypeNameOrdinal))
        {
            return reader.GetString(systemTypeNameOrdinal);
        }

        // CLR UDTs and alias types report no system type name; surface the user type name so the
        // unsupported-type error names the actual type.
        return reader.IsDBNull(userTypeNameOrdinal) ? string.Empty : reader.GetString(userTypeNameOrdinal);
    }
}
