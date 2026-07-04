using Ledger.Domain.Exceptions;

namespace Ledger.Domain;

public sealed record Transaction
{
    public Guid Id { get; }
    public DateTimeOffset OccurredAt { get; }
    public string Description { get; }
    public IReadOnlyList<Posting> Postings { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }

    public Transaction(
        Guid id,
        DateTimeOffset occurredAt,
        string description,
        IReadOnlyList<Posting> postings,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (postings is null || postings.Count == 0)
            throw new ArgumentException("Transaction must have at least one posting", nameof(postings));

        var byCurrency = postings.GroupBy(p => p.Amount.Currency);

        foreach (var group in byCurrency)
        {
            var sumDebits = group
                .Where(p => p.Debit is not null)
                .Sum(p => p.Debit!.AmountCents);

            var sumCredits = group
                .Where(p => p.Credit is not null)
                .Sum(p => p.Credit!.AmountCents);

            if (sumDebits != sumCredits)
                throw new UnbalancedTransactionException(group.Key, sumDebits, sumCredits);
        }

        Id = id;
        OccurredAt = occurredAt;
        Description = description;
        Postings = postings;
        Metadata = metadata ?? new Dictionary<string, string>();
    }
}
