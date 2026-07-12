namespace SqlBound.Migrations;

/// <summary>
/// Thrown when a migration's script fails to execute against the database. The offending
/// migration's transaction is rolled back before this is raised, so no partial change or ledger row
/// survives. The database's own exception is the <see cref="Exception.InnerException"/>.
/// </summary>
public sealed class MigrationExecutionException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="MigrationExecutionException"/> class.</summary>
    /// <param name="version">The version of the migration that failed.</param>
    /// <param name="name">The name of the migration that failed.</param>
    /// <param name="innerException">The database exception that caused the failure.</param>
    public MigrationExecutionException(long version, string name, Exception innerException)
        : base($"migration {version}_{name} failed: {innerException.Message}", innerException)
    {
        Version = version;
        Name = name;
    }

    /// <summary>The version of the migration that failed.</summary>
    public long Version { get; }

    /// <summary>The name of the migration that failed.</summary>
    public string Name { get; }
}
