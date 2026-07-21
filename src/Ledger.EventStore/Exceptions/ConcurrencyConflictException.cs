namespace Ledger.EventStore.Exceptions;

public sealed class ConcurrencyConflictException : InvalidOperationException
{
    public Guid StreamId { get; }
    public int ExpectedVersion { get; }
    public int ActualVersion { get; }

    public ConcurrencyConflictException(Guid streamId, int expectedVersion, int actualVersion)
        : base($"Conflict in stream {streamId}: caller expected version {expectedVersion}, actual is {actualVersion}")
    {
        StreamId = streamId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }
}