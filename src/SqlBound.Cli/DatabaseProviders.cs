namespace SqlBound.Cli;

/// <summary>The <c>provider</c> tags a snapshot can carry, shared between URL dispatch and serialization.</summary>
internal static class DatabaseProviders
{
    public const string SqlServer = "sqlserver";
    public const string Sqlite = "sqlite";
    public const string Postgres = "postgres";
}
