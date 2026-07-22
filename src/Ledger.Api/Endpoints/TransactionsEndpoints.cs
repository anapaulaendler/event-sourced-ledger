using Ledger.Api.Contracts;
using Ledger.Domain;
using Ledger.Domain.Events;
using Ledger.EventStore.Interfaces;

namespace Ledger.Api.Endpoints;

public static class TransactionsEndpoints
{
    public static void MapTransactionsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/transactions", async (PostTransactionRequest req, IEventStore store, CancellationToken ct) =>
        {
            var postings = req.Postings
                .Select(p =>
                {
                    if ((p.Debit is null) == (p.Credit is null))
                        throw new ArgumentException("Posting must have exactly one of debit or credit");
                    return p.Debit is not null
                        ? Posting.FromDebit(p.AccountId, new Money(p.Debit.AmountCents, p.Debit.Currency))
                        : Posting.FromCredit(p.AccountId, new Money(p.Credit!.AmountCents, p.Credit.Currency));
                })
                .ToList();

            // ana: por que 400 e não 422 quando unbalanced? escreve a defesa no README/blog.
            var transaction = new Transaction(
                Guid.NewGuid(),
                req.OccurredAt,
                req.Description,
                postings,
                req.Metadata ?? new Dictionary<string, string>());

            var evt = new TransactionPosted
            {
                TransactionId = transaction.Id,
                OccurredAt = transaction.OccurredAt,
                Description = transaction.Description,
                Postings = transaction.Postings.Select(p => new PostingSnapshot(
                    p.AccountId,
                    p.Debit?.AmountCents,
                    p.Credit?.AmountCents,
                    p.Amount.Currency)).ToList()
            };

            await store.AppendAsync(transaction.Id, expectedVersion: -1, [evt], ct);

            return Results.Created($"/transactions/{transaction.Id}", new { transactionId = transaction.Id });
        });

        app.MapPost("/transactions/{originalId:guid}/reversal", async (
            Guid originalId, ReversalRequest req, IEventStore store, CancellationToken ct) =>
        {
            var reversalId = Guid.NewGuid();
            var evt = new TransactionReversed
            {
                TransactionId = reversalId,
                OriginalTransactionId = originalId,
                Reason = req.Reason
            };
            await store.AppendAsync(reversalId, expectedVersion: -1, [evt], ct);
            return Results.Created($"/transactions/{reversalId}", new { reversalTransactionId = reversalId });
        });
    }
}
