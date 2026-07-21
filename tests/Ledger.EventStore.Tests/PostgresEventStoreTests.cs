using Ledger.Domain.Events;
using Ledger.EventStore;
using Ledger.EventStore.Exceptions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Ledger.EventStore.Tests;

public sealed class PostgresEventStoreTests : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;
    private NpgsqlDataSource _dataSource = null!;
    private PostgresEventStore _store = null!;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("docker.io/library/postgres:16")
            .WithDatabase("ledger_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        await _postgres.StartAsync();

        _dataSource = NpgsqlDataSource.Create(_postgres.GetConnectionString());

        var migrationSql = await File.ReadAllTextAsync("../../../../../src/Ledger.EventStore/Migrations/001_CreateEventsTable.sql");

        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(migrationSql, conn);
        await cmd.ExecuteNonQueryAsync();

        _store = new PostgresEventStore(_dataSource);
    }

    public async Task DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Append_To_New_Stream_Succeeds()
    {
        var streamId = Guid.NewGuid();
        var evt = new AccountOpened
        {
            AccountId = Guid.NewGuid(),
            Code = "1.1.01",
            Name = "Caixa",
            Currency = "BRL"
        };

        await _store.AppendAsync(streamId, expectedVersion: -1, [evt]);

        var read = await _store.ReadStreamAsync(streamId);
        Assert.Single(read);
        Assert.Equal(0, read[0].Version);
        Assert.Equal("AccountOpened", read[0].Type);
    }

    [Fact]
    public async Task Append_With_Wrong_Expected_Version_Throws()
    {
        var streamId = Guid.NewGuid();
        var evt = new AccountOpened
        {
            AccountId = Guid.NewGuid(),
            Code = "1.1.02",
            Name = "Conta X",
            Currency = "BRL"
        };

        await _store.AppendAsync(streamId, expectedVersion: -1, [evt]);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(async () => await _store.AppendAsync(streamId, expectedVersion: -1, [evt]));
    }

    [Fact]
    public async Task ReadStream_Of_Nonexistent_Stream_Returns_Empty()
    {
        var nonexistent = Guid.NewGuid();

        var read = await _store.ReadStreamAsync(nonexistent);

        Assert.Empty(read);
    }

    [Fact]
    public async Task Append_Multiple_Events_Assigns_Sequential_Versions()
    {
        var streamId = Guid.NewGuid();
        var events = new DomainEvent[]
        {
            new AccountOpened { AccountId = Guid.NewGuid(), Code = "1", Name = "A", Currency = "BRL" },
            new TransactionReversed { TransactionId = Guid.NewGuid(), OriginalTransactionId = Guid.NewGuid(), Reason = "r1" },
            new TransactionReversed { TransactionId = Guid.NewGuid(), OriginalTransactionId = Guid.NewGuid(), Reason = "r2" }
        };

        await _store.AppendAsync(streamId, expectedVersion: -1, events);

        var read = await _store.ReadStreamAsync(streamId);
        Assert.Equal(3, read.Count);
        Assert.Equal(0, read[0].Version);
        Assert.Equal(1, read[1].Version);
        Assert.Equal(2, read[2].Version);
    }
}
