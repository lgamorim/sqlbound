using Microsoft.Data.SqlClient;

namespace SqlBound.Cli;

/// <summary>
/// Resolves the value of <c>SQLBOUND_DATABASE_URL</c> (or <c>--connection</c>) into an ADO.NET
/// connection string. Accepts either a raw connection string, passed through verbatim, or a
/// CI-friendly URL of the form <c>sqlserver://user:pass@host:port/database?Option=value</c>.
/// </summary>
internal static class DatabaseUrl
{
    public const string EnvironmentVariable = "SQLBOUND_DATABASE_URL";

    private const string UrlScheme = "sqlserver://";

    public static string ToConnectionString(string value)
    {
        if (!value.StartsWith(UrlScheme, StringComparison.OrdinalIgnoreCase))
        {
            ValidateConnectionString(value);
            return value;
        }

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

    private static void ValidateConnectionString(string value)
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
