using Dapper;
using Ledger.Idempotency;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Ledger.Idempotency.Tests;

public sealed class IdempotencyStoreTests : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;
    private NpgsqlDataSource _dataSource = null!;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("docker.io/library/postgres:16")
            .WithDatabase("ledger_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        await _postgres.StartAsync();
        _dataSource = NpgsqlDataSource.Create(_postgres.GetConnectionString());

        var migrationSql = await File.ReadAllTextAsync(
            "../../../../../src/Ledger.Idempotency/Migrations/002_CreateIdempotencyTable.sql");
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(migrationSql, conn);
        await cmd.ExecuteNonQueryAsync();

        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public async Task DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Insert_Then_TryGet_Returns_Record()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var record = new IdempotencyRecord
        {
            Key = "test-key-1",
            RequestHash = "abc123",
            ResponseStatus = 201,
            ResponseBody = """{"ok":true}""",
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        await IdempotencyStore.InsertAsync(conn, tx, record, default);
        var read = await IdempotencyStore.TryGetAsync(conn, tx, "test-key-1", default);

        Assert.NotNull(read);
        Assert.Equal("abc123", read!.RequestHash);
        Assert.Equal(201, read.ResponseStatus);

        await tx.CommitAsync();
    }

    [Fact]
    public async Task TryGet_Nonexistent_Returns_Null()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var read = await IdempotencyStore.TryGetAsync(conn, tx, "nonexistent", default);

        Assert.Null(read);
    }

    [Fact]
    public async Task TryGet_Expired_Returns_Null()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var expired = new IdempotencyRecord
        {
            Key = "expired-key",
            RequestHash = "abc",
            ResponseStatus = 201,
            ResponseBody = "{}",
            ExpiresAt = DateTime.UtcNow.AddSeconds(-1)
        };

        await IdempotencyStore.InsertAsync(conn, tx, expired, default);
        var read = await IdempotencyStore.TryGetAsync(conn, tx, "expired-key", default);

        Assert.Null(read);

        await tx.CommitAsync();
    }
}
