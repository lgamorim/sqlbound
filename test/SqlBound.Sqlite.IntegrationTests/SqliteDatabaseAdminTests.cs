using Microsoft.Data.Sqlite;

namespace SqlBound.Sqlite.IntegrationTests;

/// <summary>
/// Exercises <see cref="SqliteDatabaseAdmin"/>: for SQLite, creating a database materializes its
/// file and dropping it deletes the file.
/// </summary>
public sealed class SqliteDatabaseAdminTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"sqlbound-admin-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    [Fact]
    public async Task Should_CreateDatabaseFile_When_Create()
    {
        await new SqliteDatabaseAdmin().CreateAsync(ConnectionString, Token);

        Assert.True(File.Exists(_path));
    }

    [Fact]
    public async Task Should_DeleteDatabaseFile_When_Drop()
    {
        await new SqliteDatabaseAdmin().CreateAsync(ConnectionString, Token);

        await new SqliteDatabaseAdmin().DropAsync(ConnectionString, Token);

        Assert.False(File.Exists(_path));
    }

    [Fact]
    public async Task Should_NotThrow_When_DropCalledForMissingFile()
    {
        await new SqliteDatabaseAdmin().DropAsync(ConnectionString, Token);
    }

    [Fact]
    public async Task Should_ThrowArgumentException_When_ConnectionStringNamesNoDataSource()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => new SqliteDatabaseAdmin().CreateAsync(new SqliteConnectionStringBuilder().ConnectionString, Token));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = _path }.ConnectionString;
}
