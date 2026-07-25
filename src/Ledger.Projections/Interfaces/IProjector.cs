using Ledger.EventStore.Events;
using Npgsql;

namespace Ledger.Projections.Interfaces;

public interface IProjector
{
    string Name { get; }

    Task ApplyAsync(StoredEvent evt, NpgsqlConnection conn, NpgsqlTransaction tx, CancellationToken ct);
}
