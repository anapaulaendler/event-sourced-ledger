using Dapper;
using Ledger.EventStore;
using Ledger.EventStore.Interfaces;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Ledger.Outbox.Tests;

public sealed class OutboxFixture : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;

    public NpgsqlDataSource DataSource { get; private set; } = null!;
    public IEventStore Store { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("docker.io/library/postgres:16")
            .WithDatabase("ledger_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
        await _postgres.StartAsync();

        DefaultTypeMap.MatchNamesWithUnderscores = true;
        DataSource = NpgsqlDataSource.Create(_postgres.GetConnectionString());

        await using var conn = await DataSource.OpenConnectionAsync();
        foreach (var path in new[]
        {
            "../../../../../src/Ledger.EventStore/Migrations/001_CreateEventsTable.sql",
            "../../../../../src/Ledger.Outbox/Migrations/007_CreateOutboxTable.sql"
        })
        {
            var sql = await File.ReadAllTextAsync(path);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        Store = new PostgresEventStore(DataSource);
    }

    /// <summary>Insere um evento cru, disparando a trigger enqueue_outbox.</summary>
    public async Task<long> AppendRawEventAsync(Guid streamId, int version, string type, string payloadJson, Guid? correlationId = null)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.ExecuteScalarAsync<long>(
            """
            INSERT INTO events (stream_id, version, type, payload, correlation_id)
            VALUES (@streamId, @version, @type, @payloadJson::jsonb, @correlationId)
            RETURNING sequence
            """,
            new { streamId, version, type, payloadJson, correlationId });
    }

    public async Task ResetAsync()
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        await conn.ExecuteAsync("TRUNCATE outbox, events RESTART IDENTITY CASCADE");
    }

    public async Task DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(nameof(OutboxCollection))]
public sealed class OutboxCollection : ICollectionFixture<OutboxFixture>;
