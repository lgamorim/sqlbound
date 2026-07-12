using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using SqlBound.Migrations;
using SqlBound.Npgsql;
using SqlBound.Sqlite;
using SqlBound.SqlServer;

namespace SqlBound.Cli;

/// <summary>
/// Resolves the per-provider services the connected commands need: the ADO.NET connection, the
/// migration ledger, and the database administrator. Connection creation covers every provider (the
/// drivers ship in the CLI); the ledger and administrator are wired provider by provider as M15
/// lands them, with a clear error for any not yet supported.
/// </summary>
internal static class ProviderServices
{
    public static DbConnection CreateConnection(DatabaseTarget target) => target.Provider switch
    {
        DatabaseProviders.Sqlite => new SqliteConnection(target.ConnectionString),
        DatabaseProviders.Postgres => new NpgsqlConnection(target.ConnectionString),
        DatabaseProviders.MySql => new MySqlConnection(target.ConnectionString),
        _ => new SqlConnection(target.ConnectionString),
    };

    public static IMigrationLedger Ledger(string provider) => provider switch
    {
        DatabaseProviders.SqlServer => new SqlServerMigrationLedger(),
        DatabaseProviders.Sqlite => new SqliteMigrationLedger(),
        DatabaseProviders.Postgres => new NpgsqlMigrationLedger(),
        _ => throw Unsupported(provider),
    };

    public static IDatabaseAdmin DatabaseAdmin(string provider) => provider switch
    {
        DatabaseProviders.SqlServer => new SqlServerDatabaseAdmin(),
        DatabaseProviders.Sqlite => new SqliteDatabaseAdmin(),
        DatabaseProviders.Postgres => new NpgsqlDatabaseAdmin(),
        _ => throw Unsupported(provider),
    };

    private static NotSupportedException Unsupported(string provider) =>
        new($"the '{provider}' provider is not supported yet.");
}
