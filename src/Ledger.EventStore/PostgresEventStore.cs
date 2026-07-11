using System.Text.Json;
using Dapper;
using Ledger.Domain.Events;
using Ledger.EventStore.Events;
using Ledger.EventStore.Exceptions;
using Ledger.EventStore.Interfaces;
using Npgsql;

namespace Ledger.EventStore;

public sealed class PostgresEventStore : IEventStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresEventStore(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task AppendAsync(Guid streamId, int expectedVersion, IReadOnlyList<DomainEvent> events, CancellationToken ct = default)
    {
        if (events.Count == 0) return;

        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        await using var tx = await conn.BeginTransactionAsync(ct);

        var currentVersion = await GetCurrentVersionAsync(conn, tx, streamId, ct);

        if (currentVersion != expectedVersion)
            throw new ConcurrencyConflictException(streamId, expectedVersion, currentVersion);

        for (int i = 0; i < events.Count; i++)
        {
            await InsertEventAsync(conn, tx, streamId, version: currentVersion + 1 + i, events[i], ct);
        }

        await tx.CommitAsync(ct);
    }

    private static async Task<int> GetCurrentVersionAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid streamId, CancellationToken ct)
    {
        return await conn.QuerySingleAsync<int>(new CommandDefinition(SqlQueries.GetMaxVersion, new { stream_id = streamId }, tx, cancellationToken: ct));
    }

    private static async Task InsertEventAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid streamId, int version, DomainEvent evt, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(evt, evt.GetType());

        var parameters = new
        {
            stream_id = streamId,
            version,
            type = evt.GetType().Name,
            payload,
            occurred_at = evt.OccurredAt
        };

        try
        {
            await conn.ExecuteAsync(new CommandDefinition(SqlQueries.InsertEvent, parameters, tx, cancellationToken: ct));
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            throw new ConcurrencyConflictException(streamId, version - 1, version);
        }
    }

    public Task<IReadOnlyList<StoredEvent>> ReadStreamAsync(Guid streamId, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<StoredEvent>> ReadAllAsync(long fromSequence, int max, CancellationToken ct = default)
        => throw new NotImplementedException();
}
