namespace Ledger.EventStore.Events;

public sealed record StoredEvent(
    long Sequence,
    Guid StreamId,
    int Version,
    string Type,
    string PayloadJson,
    DateTimeOffset OccurredAt,
    Guid? CorrelationId);