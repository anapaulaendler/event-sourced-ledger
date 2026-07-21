using Ledger.Domain.Events;
using Ledger.EventStore.Events;

namespace Ledger.EventStore.Interfaces;

public interface IEventStore
{
    Task AppendAsync(Guid streamId, int expectedVersion, IReadOnlyList<DomainEvent> events, CancellationToken ct = default);

    Task<IReadOnlyList<StoredEvent>> ReadStreamAsync(Guid streamId, CancellationToken ct = default);

    Task<IReadOnlyList<StoredEvent>> ReadAllAsync(long fromSequence, int max, CancellationToken ct = default);
}