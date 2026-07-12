using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;

namespace SqlBound.Cli;

/// <summary>A resolved database target: which provider to describe against, and the ADO.NET connection string to use.</summary>
internal readonly record struct DatabaseTarget(string Provider, string ConnectionString);

/// <summary>
/// Resolves the value of <c>SQLBOUND_DATABASE_URL</c> (or <c>--connection</c>) into a
/// <see cref="DatabaseTarget"/>. Accepts a <c>sqlserver://</c>, <c>sqlite://</c>,
/// <c>postgresql://</c>/<c>postgres://</c>, or <c>mysql://</c> URL, or - for backward
/// compatibility with SQL Server's original single-provider convention - a raw ADO.NET connection
/// string passed through verbatim as SQL Server.
/// </summary>
internal static class DatabaseUrl
{
    public const string EnvironmentVariable = "SQLBOUND_DATABASE_URL";

    private const string SqlServerScheme = "sqlserver://";
    private const string SqliteScheme = "sqlite://";
    private const string PostgresScheme = "postgresql://";
    private const string PostgresSchemeAlias = "postgres://";
    private const string MySqlScheme = "mysql://";
    private const uint MySqlDefaultPort = 3306;

    public static DatabaseTarget Resolve(string value)
    {
        if (value.StartsWith(SqliteScheme, StringComparison.OrdinalIgnoreCase))
        {
            return new DatabaseTarget(DatabaseProviders.Sqlite, ToSqliteConnectionString(value));
        }

        if (value.StartsWith(PostgresScheme, StringComparison.OrdinalIgnoreCase))
        {
            return new DatabaseTarget(DatabaseProviders.Postgres, ToPostgresConnectionString(value, PostgresScheme));
        }

        if (value.StartsWith(PostgresSchemeAlias, StringComparison.OrdinalIgnoreCase))
        {
            return new DatabaseTarget(DatabaseProviders.Postgres, ToPostgresConnectionString(value, PostgresSchemeAlias));
        }

        if (value.StartsWith(MySqlScheme, StringComparison.OrdinalIgnoreCase))
        {
            return new DatabaseTarget(DatabaseProviders.MySql, ToMySqlConnectionString(value));
        }

        if (!value.StartsWith(SqlServerScheme, StringComparison.OrdinalIgnoreCase))
        {
            ValidateSqlServerConnectionString(value);
            return new DatabaseTarget(DatabaseProviders.SqlServer, value);
        }

        return new DatabaseTarget(DatabaseProviders.SqlServer, ToSqlServerConnectionString(value));
    }

    private static string ToSqliteConnectionString(string value)
    {
        var path = value.Substring(SqliteScheme.Length);
        if (path.Length == 0)
        {
            throw new ArgumentException($"'{value}' is not a valid sqlite:// URL: no path given.");
        }

        return new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString;
    }

    private static string ToSqlServerConnectionString(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var url) || url.Host.Length == 0)
        {
            throw new ArgumentException($"'{value}' is not a valid sqlserver:// URL.");
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = url.IsDefaultPort ? url.Host : $"{url.Host},{url.Port}",
        };

        var database = url.AbsolutePath.TrimStart('/');
        if (database.Length > 0)
        {
            builder.InitialCatalog = Uri.UnescapeDataString(database);
        }

        if (url.UserInfo.Length > 0)
        {
            var separator = url.UserInfo.IndexOf(':');
            builder.UserID = Uri.UnescapeDataString(separator < 0 ? url.UserInfo : url.UserInfo[..separator]);
            if (separator >= 0)
            {
                builder.Password = Uri.UnescapeDataString(url.UserInfo[(separator + 1)..]);
            }
        }
        else
        {
            builder.IntegratedSecurity = true;
        }

        foreach (var option in url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = option.IndexOf('=');
            var key = Uri.UnescapeDataString(separator < 0 ? option : option[..separator]);
            var optionValue = separator < 0 ? string.Empty : Uri.UnescapeDataString(option[(separator + 1)..]);
            try
            {
                builder[key] = optionValue;
            }
            catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or FormatException)
            {
                throw new ArgumentException($"Unsupported connection option '{key}' in '{value}'.", exception);
            }
        }

        return builder.ConnectionString;
    }

    private static string ToPostgresConnectionString(string value, string scheme)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var url) || url.Host.Length == 0)
        {
            throw new ArgumentException($"'{value}' is not a valid {scheme.TrimEnd('/')} URL.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = url.Host,
            Port = url.IsDefaultPort ? NpgsqlConnection.DefaultPort : url.Port,
        };

        var database = url.AbsolutePath.TrimStart('/');
        if (database.Length > 0)
        {
            builder.Database = Uri.UnescapeDataString(database);
        }

        if (url.UserInfo.Length > 0)
        {
            var separator = url.UserInfo.IndexOf(':');
            builder.Username = Uri.UnescapeDataString(separator < 0 ? url.UserInfo : url.UserInfo[..separator]);
            if (separator >= 0)
            {
                builder.Password = Uri.UnescapeDataString(url.UserInfo[(separator + 1)..]);
            }
        }

        foreach (var option in url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = option.IndexOf('=');
            var key = Uri.UnescapeDataString(separator < 0 ? option : option[..separator]);
            var optionValue = separator < 0 ? string.Empty : Uri.UnescapeDataString(option[(separator + 1)..]);
            try
            {
                builder[key] = optionValue;
            }
            catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or FormatException)
            {
                throw new ArgumentException($"Unsupported connection option '{key}' in '{value}'.", exception);
            }
        }

        return builder.ConnectionString;
    }

    private static string ToMySqlConnectionString(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var url) || url.Host.Length == 0)
        {
            throw new ArgumentException($"'{value}' is not a valid mysql:// URL.");
        }

        var builder = new MySqlConnectionStringBuilder
        {
            Server = url.Host,
            Port = url.IsDefaultPort ? MySqlDefaultPort : (uint)url.Port,
        };

        var database = url.AbsolutePath.TrimStart('/');
        if (database.Length > 0)
        {
            builder.Database = Uri.UnescapeDataString(database);
        }

        if (url.UserInfo.Length > 0)
        {
            var separator = url.UserInfo.IndexOf(':');
            builder.UserID = Uri.UnescapeDataString(separator < 0 ? url.UserInfo : url.UserInfo[..separator]);
            if (separator >= 0)
            {
                builder.Password = Uri.UnescapeDataString(url.UserInfo[(separator + 1)..]);
            }
        }

        foreach (var option in url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = option.IndexOf('=');
            var key = Uri.UnescapeDataString(separator < 0 ? option : option[..separator]);
            var optionValue = separator < 0 ? string.Empty : Uri.UnescapeDataString(option[(separator + 1)..]);
            try
            {
                builder[key] = optionValue;
            }
            catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or FormatException)
            {
                throw new ArgumentException($"Unsupported connection option '{key}' in '{value}'.", exception);
            }
        }

        return builder.ConnectionString;
    }

    private static void ValidateSqlServerConnectionString(string value)
    {
        try
        {
            _ = new SqlConnectionStringBuilder(value);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or KeyNotFoundException)
        {
            throw new ArgumentException($"'{value}' is not a valid connection string.", exception);
        }
    }
}
