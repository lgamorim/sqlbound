using System.Data;
using System.Data.Common;
using global::Npgsql;
using global::Npgsql.Schema;
using SqlBound.Introspection;

namespace SqlBound.Npgsql;

/// <summary>
/// Describes a command text against a live PostgreSQL database using the extended query
/// protocol's <c>Describe</c> message (<c>CommandBehavior.SchemaOnly</c>, which never executes
/// the statement) plus one additional catalog lookup per direct table column for nullability.
/// Per ADR 0001 this round-trip belongs exclusively to the CLI <c>prepare</c> step (or the opt-in
/// MSBuild task) — it must never run inside the Roslyn analyzer or at application runtime.
/// </summary>
public sealed class NpgsqlQueryDescriber : IQueryDescriber
{
    /// <summary>Describes <paramref name="commandText"/>'s result columns and parameters.</summary>
    /// <param name="connection">An open <see cref="NpgsqlConnection"/> to the database to describe against.</param>
    /// <param name="commandText">The command text to describe.</param>
    /// <param name="cancellationToken">Cancels the describe round-trips.</param>
    /// <exception cref="ArgumentException"><paramref name="connection"/> is not an <see cref="NpgsqlConnection"/>.</exception>
    /// <exception cref="SqlBoundDescribeException">PostgreSQL could not describe the command, or a described type has no SqlBound-supported mapping.</exception>
    public async Task<QueryDescription> DescribeAsync(
        DbConnection connection, string commandText, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);
        if (connection is not NpgsqlConnection npgsqlConnection)
        {
            throw new ArgumentException(
                $"NpgsqlQueryDescriber requires a {typeof(NpgsqlConnection)}, but received a {connection.GetType()}.",
                nameof(connection));
        }

        var command = npgsqlConnection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.CommandText = commandText;
            try
            {
                // Deriving parameters first, on the same command, both describes the parameters
                // and reliably primes Npgsql's @name -> $N rewriter for the column describe below -
                // an ad hoc command with multiple undeclared named parameters otherwise fails to
                // rewrite correctly.
                NpgsqlCommandBuilder.DeriveParameters(command);
                var parameters = DescribeParameters(command, commandText);
                var columns = await DescribeColumnsAsync(npgsqlConnection, command, commandText, cancellationToken)
                    .ConfigureAwait(false);
                return new QueryDescription(columns, parameters);
            }
            catch (PostgresException exception)
            {
                throw new SqlBoundDescribeException(
                    $"PostgreSQL could not describe the command: {exception.MessageText}", commandText, exception);
            }
            catch (InvalidOperationException exception)
            {
                throw new SqlBoundDescribeException(
                    $"PostgreSQL could not describe the command's parameters: {exception.Message}", commandText, exception);
            }
        }
    }

    private static async Task<IReadOnlyList<DescribedColumn>> DescribeColumnsAsync(
        NpgsqlConnection connection, NpgsqlCommand command, string commandText, CancellationToken cancellationToken)
    {
        List<NpgsqlDbColumn> schema;
        var reader = await command.ExecuteReaderAsync(CommandBehavior.SchemaOnly, cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            schema = (await reader.GetColumnSchemaAsync(cancellationToken).ConfigureAwait(false)).Cast<NpgsqlDbColumn>().ToList();
        }

        // The nullability lookup opens its own command on the same connection - it must run only
        // after the SchemaOnly reader above is fully disposed, since Npgsql (unlike SQL Server)
        // does not support running two commands concurrently on one connection.
        var columns = new List<DescribedColumn>(schema.Count);
        for (var ordinal = 0; ordinal < schema.Count; ordinal++)
        {
            var column = schema[ordinal];
            var name = column.ColumnName;
            var dataTypeName = column.DataTypeName ?? string.Empty;
            if (!PostgresTypeMap.TryMap(dataTypeName, out var clrTypeText))
            {
                throw new SqlBoundDescribeException(
                    $"Column '{name}' has type '{dataTypeName}', which SqlBound cannot materialize.",
                    commandText);
            }

            var isNullable = await IsNullableAsync(connection, column, cancellationToken).ConfigureAwait(false);
            columns.Add(new DescribedColumn(ordinal, name, dataTypeName, clrTypeText, isNullable));
        }

        return columns;
    }

    private static async Task<bool> IsNullableAsync(NpgsqlConnection connection, NpgsqlDbColumn column, CancellationToken cancellationToken)
    {
        if (column.TableOID == 0 || column.ColumnAttributeNumber is null)
        {
            // A computed/aggregate/expression column has no source table to check against - default
            // to nullable (the safe direction), the same convention the other providers use.
            return true;
        }

        var lookup = connection.CreateCommand();
        await using (lookup.ConfigureAwait(false))
        {
            lookup.CommandText = "SELECT attnotnull FROM pg_attribute WHERE attrelid = @tableOid AND attnum = @attNum";
            lookup.Parameters.AddWithValue("tableOid", (long)column.TableOID);
            lookup.Parameters.AddWithValue("attNum", column.ColumnAttributeNumber.Value);
            var notNull = (bool)(await lookup.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
            return !notNull;
        }
    }

    private static IReadOnlyList<DescribedParameter> DescribeParameters(NpgsqlCommand command, string commandText)
    {
        var parameters = new List<DescribedParameter>(command.Parameters.Count);
        foreach (NpgsqlParameter parameter in command.Parameters)
        {
            var dataTypeName = parameter.DataTypeName ?? string.Empty;
            if (!PostgresTypeMap.TryMap(dataTypeName, out var clrTypeText))
            {
                throw new SqlBoundDescribeException(
                    $"Parameter '{parameter.ParameterName}' has suggested type '{dataTypeName}', which SqlBound cannot bind.",
                    commandText);
            }

            parameters.Add(new DescribedParameter(parameter.ParameterName, dataTypeName, clrTypeText));
        }

        return parameters;
    }
}
