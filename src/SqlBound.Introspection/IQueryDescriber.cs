using System.Data.Common;

namespace SqlBound.Introspection;

/// <summary>
/// Describes a command text's result columns and parameters against a live database connection.
/// Implemented once per provider (SQL Server, SQLite, ...). Per ADR 0001 this round-trip belongs
/// exclusively to the CLI <c>prepare</c> step (or an opt-in MSBuild task) — it must never run
/// inside the Roslyn analyzer or at application runtime.
/// </summary>
public interface IQueryDescriber
{
    /// <summary>Describes <paramref name="commandText"/>'s result columns and parameters.</summary>
    /// <param name="connection">An open connection, of the type this describer's provider expects, to the database to describe against.</param>
    /// <param name="commandText">The command text to describe.</param>
    /// <param name="cancellationToken">Cancels the describe round-trips.</param>
    /// <exception cref="SqlBoundDescribeException">The provider could not describe the command, or a described type has no SqlBound-supported mapping.</exception>
    Task<QueryDescription> DescribeAsync(DbConnection connection, string commandText, CancellationToken cancellationToken = default);
}
