using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using SqlBound.Introspection;
using SqlBound.Sqlite;
using SqlBound.SqlServer;

namespace SqlBound.Cli;

/// <summary>
/// The <c>prepare</c> workflow: discover command texts, describe each against the database, and
/// reconcile <c>.sqlbound/</c> (or, with <c>--check</c>, report what a run would
/// change). Exit codes: 0 success, 1 discovery/describe/connection failure, 2 snapshot drift
/// under <c>--check</c>. A run with describe failures never touches disk — pruning based on a
/// partial result would delete the snapshots of the very queries that failed.
/// </summary>
internal static class PrepareRunner
{
    public static async Task<int> RunAsync(
        string projectDirectory, string databaseValue, bool check, TextWriter output, CancellationToken cancellationToken)
    {
        DatabaseTarget target;
        try
        {
            target = DatabaseUrl.Resolve(databaseValue);
        }
        catch (ArgumentException exception)
        {
            output.WriteLine($"error: {exception.Message}");
            return 1;
        }

        var discovery = QueryDiscovery.DiscoverFromDirectory(projectDirectory);
        foreach (var warning in discovery.Warnings)
        {
            output.WriteLine($"warning: {warning}");
        }

        var connection = CreateConnection(target);
        await using (connection.ConfigureAwait(false))
        {
            try
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is SqlException or SqliteException or InvalidOperationException)
            {
                output.WriteLine($"error: cannot connect to the database: {exception.Message}");
                return 1;
            }

            var describer = CreateDescriber(target);
            var desired = new Dictionary<string, string>();
            var failures = 0;
            foreach (var group in discovery.Queries.GroupBy(query => query.CommandText, StringComparer.Ordinal))
            {
                var methods = string.Join(", ", group.Select(query => query.MethodName).Distinct());
                try
                {
                    var description = await describer
                        .DescribeAsync(connection, group.Key, cancellationToken)
                        .ConfigureAwait(false);
                    desired[SnapshotWriter.FileName(group.Key)] =
                        SnapshotWriter.Serialize(group.Key, description, target.Provider);
                }
                catch (SqlBoundDescribeException exception)
                {
                    failures++;
                    output.WriteLine($"error: {methods}: {exception.Message}");
                }
            }

            if (failures > 0)
            {
                output.WriteLine($"prepare failed: {failures} of {failures + desired.Count} queries could not be described; no snapshots were written.");
                return 1;
            }

            var snapshotDirectory = Path.Combine(projectDirectory, SnapshotStore.DirectoryName);
            if (check)
            {
                var difference = SnapshotStore.Compare(snapshotDirectory, desired);
                foreach (var fileName in difference.Added)
                {
                    output.WriteLine($"stale: {fileName} is missing");
                }

                foreach (var fileName in difference.Changed)
                {
                    output.WriteLine($"stale: {fileName} does not match the database");
                }

                foreach (var fileName in difference.Orphaned)
                {
                    output.WriteLine($"stale: {fileName} has no matching query");
                }

                if (difference.HasDrift)
                {
                    output.WriteLine("check failed: snapshots are stale; re-run 'dotnet sqlbound prepare' and commit the result.");
                    return 2;
                }

                output.WriteLine($"check passed: {difference.Unchanged.Count} snapshots match the database.");
                return 0;
            }

            var applied = SnapshotStore.Apply(snapshotDirectory, desired);
            output.WriteLine(
                $"prepared {desired.Count} queries: {applied.Added.Count} added, {applied.Changed.Count} updated, " +
                $"{applied.Unchanged.Count} unchanged, {applied.Orphaned.Count} removed.");
            return 0;
        }
    }

    private static DbConnection CreateConnection(DatabaseTarget target) => target.Provider switch
    {
        DatabaseProviders.Sqlite => new SqliteConnection(target.ConnectionString),
        _ => new SqlConnection(target.ConnectionString),
    };

    private static IQueryDescriber CreateDescriber(DatabaseTarget target) => target.Provider switch
    {
        DatabaseProviders.Sqlite => new SqliteQueryDescriber(),
        _ => new SqlServerQueryDescriber(),
    };
}
