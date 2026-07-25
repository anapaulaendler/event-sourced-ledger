namespace Ledger.Domain.Events;

public sealed record TransactionReversed : DomainEvent
{
    public Guid TransactionId { get; init; }
    public Guid OriginalTransactionId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public IReadOnlyList<PostingSnapshot> Postings { get; init; } = [];
}
