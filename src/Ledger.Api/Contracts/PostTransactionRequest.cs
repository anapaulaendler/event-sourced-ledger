namespace Ledger.Api.Contracts;

public sealed record PostTransactionRequest
{
    public DateTimeOffset OccurredAt { get; init; }
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<PostingRequest> Postings { get; init; } = [];
    public Dictionary<string, string>? Metadata { get; init; }
}

public sealed record PostingRequest
{
    public Guid AccountId { get; init; }
    public MoneyRequest? Debit { get; init; }
    public MoneyRequest? Credit { get; init; }
}

public sealed record MoneyRequest
{
    public long AmountCents { get; init; }
    public string Currency { get; init; } = string.Empty;
}
