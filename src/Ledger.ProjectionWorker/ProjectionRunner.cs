using Ledger.EventStore.Interfaces;
using Ledger.Projections;
using Ledger.Projections.Interfaces;
using Npgsql;

namespace Ledger.ProjectionWorker;

public sealed class ProjectionRunner
{
    private const int BATCH_SIZE = 100;

    private readonly NpgsqlDataSource _dataSource;
    private readonly IEventStore _store;
    private readonly IEnumerable<IProjector> _projectors;
    private readonly ILogger<ProjectionRunner> _logger;

    public ProjectionRunner(NpgsqlDataSource dataSource, IEventStore store, IEnumerable<IProjector> projectors, ILogger<ProjectionRunner> logger)
    {
        _dataSource = dataSource;
        _store = store;
        _projectors = projectors;
        _logger = logger;
    }

    public async Task<bool> RunOnceAsync(CancellationToken ct)
    {
        var anyBatchFull = false;

        foreach (var projector in _projectors)
        {
            var batchFull = await RunProjectorAsync(projector, ct);
            anyBatchFull = anyBatchFull || batchFull;
        }

        return anyBatchFull;
    }

    private async Task<bool> RunProjectorAsync(IProjector projector, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        var lastCheckpoint = await CheckpointStore.GetAsync(conn, tx: null, projector.Name, ct);
        var events = await _store.ReadAllAsync(lastCheckpoint, BATCH_SIZE, ct);

        if (events.Count == 0) return false;

        foreach (var evt in events)
        {
            await using var tx = await conn.BeginTransactionAsync(ct);
            try
            {
                await projector.ApplyAsync(evt, conn, tx, ct);
                await CheckpointStore.SetAsync(conn, tx, projector.Name, evt.Sequence, ct);
                await tx.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Projector {Projector} failed at sequence {Sequence}", projector.Name, evt.Sequence);
                throw;
            }
        }

        _logger.LogInformation("Projector {Projector} applied {Count} events (up to sequence {Last})", projector.Name, events.Count, events[^1].Sequence);
        return events.Count == BATCH_SIZE;
    }
}
