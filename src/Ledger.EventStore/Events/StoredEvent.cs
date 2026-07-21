namespace Ledger.EventStore.Events;

public sealed record StoredEvent
{
    public long Sequence { get; init; }
    public Guid StreamId { get; init; }
    public int Version { get; init; }
    public string Type { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
    public Guid? CorrelationId { get; init; }
}
