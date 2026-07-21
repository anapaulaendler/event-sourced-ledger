namespace Ledger.Domain.Events;

public sealed record TransactionPosted : DomainEvent
{
    public Guid TransactionId { get; init; }
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<PostingSnapshot> Postings { get; init; } = [];
}