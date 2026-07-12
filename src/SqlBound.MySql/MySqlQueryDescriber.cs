using System.Data;
using System.Data.Common;
using global::MySqlConnector;
using SqlBound.Introspection;

namespace SqlBound.MySql;

/// <summary>
/// Describes a command text against a live MySQL database using <c>COM_STMT_PREPARE</c>
/// (<c>MySqlCommand.PrepareAsync</c> plus <c>CommandBehavior.SchemaOnly</c>), which never executes
/// the statement. Per ADR 0001 this round-trip belongs exclusively to the CLI <c>prepare</c> step
/// (or the opt-in MSBuild task) — it must never run inside the Roslyn analyzer or at application
/// runtime. Unlike the other three providers, MySQL has no server-side way to discover a
/// statement's parameter names or types: <see cref="MySqlParameterScanner"/> finds the names by
/// scanning the command text, and every parameter's <see cref="DescribedParameter.ClrTypeText"/>
/// is <see langword="null"/> — MySQL simply echoes back whatever type the caller declares, so
/// there is nothing genuine to report.
/// </summary>
public sealed class MySqlQueryDescriber : IQueryDescriber
{
    /// <summary>Describes <paramref name="commandText"/>'s result columns and parameters.</summary>
    /// <param name="connection">An open <see cref="MySqlConnection"/> to the database to describe against.</param>
    /// <param name="commandText">The command text to describe.</param>
    /// <param name="cancellationToken">Cancels the describe round-trip.</param>
    /// <exception cref="ArgumentException"><paramref name="connection"/> is not a <see cref="MySqlConnection"/>.</exception>
    /// <exception cref="SqlBoundDescribeException">MySQL could not describe the command, or a described type has no SqlBound-supported mapping.</exception>
    public async Task<QueryDescription> DescribeAsync(
        DbConnection connection, string commandText, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);
        if (connection is not MySqlConnection mySqlConnection)
        {
            throw new ArgumentException(
                $"MySqlQueryDescriber requires a {typeof(MySqlConnection)}, but received a {connection.GetType()}.",
                nameof(connection));
        }

        var parameterNames = MySqlParameterScanner.ExtractNames(commandText);
        var command = mySqlConnection.CreateCommand();
        await using (command.ConfigureAwait(false))
        {
            command.CommandText = commandText;
            foreach (var name in parameterNames)
            {
                // The declared type is never used for anything - MySQL's SchemaOnly describe
                // resolves real result-column types regardless of it, and there is no server-side
                // parameter type inference to feed it into.
                command.Parameters.Add(new MySqlParameter($"@{name}", MySqlDbType.VarChar));
            }

            try
            {
                await command.PrepareAsync(cancellationToken).ConfigureAwait(false);
                var columns = await DescribeColumnsAsync(command, commandText, cancellationToken).ConfigureAwait(false);
                var parameters = parameterNames
                    .Select(name => new DescribedParameter(name, string.Empty, ClrTypeText: null))
                    .ToList();
                return new QueryDescription(columns, parameters);
            }
            catch (MySqlException exception)
            {
                throw new SqlBoundDescribeException(
                    $"MySQL could not describe the command: {exception.Message}", commandText, exception);
            }
        }
    }

    private static async Task<IReadOnlyList<DescribedColumn>> DescribeColumnsAsync(
        MySqlCommand command, string commandText, CancellationToken cancellationToken)
    {
        var reader = await command.ExecuteReaderAsync(CommandBehavior.SchemaOnly, cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            var schema = await reader.GetColumnSchemaAsync(cancellationToken).ConfigureAwait(false);
            var columns = new List<DescribedColumn>(schema.Count);
            for (var ordinal = 0; ordinal < schema.Count; ordinal++)
            {
                var column = schema[ordinal];
                var name = column.ColumnName;
                var dataTypeName = column.DataTypeName ?? string.Empty;
                if (!MySqlTypeMap.TryMap(dataTypeName, out var clrTypeText))
                {
                    throw new SqlBoundDescribeException(
                        $"Column '{name}' has type '{dataTypeName}', which SqlBound cannot materialize.",
                        commandText);
                }

                // A column with no server-reported nullability defaults to nullable - the safe
                // direction - the same convention the other providers use.
                columns.Add(new DescribedColumn(ordinal, name, dataTypeName, clrTypeText, column.AllowDBNull ?? true));
            }

            return columns;
        }
    }
}
