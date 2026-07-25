using System.Text.Json;
using Dapper;
using Ledger.Domain.Events;
using Ledger.EventStore.Events;
using Ledger.Projections.Interfaces;
using Npgsql;

namespace Ledger.Projections;

public sealed class StatementProjector : IProjector
{
    public string Name => "statement";

    public async Task ApplyAsync(StoredEvent @event, NpgsqlConnection conn, NpgsqlTransaction tx, CancellationToken ct)
    {
        var (postings, description) = @event.Type switch
        {
            nameof(TransactionPosted) => Extract(JsonSerializer.Deserialize<TransactionPosted>(@event.PayloadJson)!),
            nameof(TransactionReversed) => ExtractReversal(JsonSerializer.Deserialize<TransactionReversed>(@event.PayloadJson)!),
            _ => (null, null)
        };

        if (postings is null) return;

        foreach (var p in postings)
        {
            var delta = (p.DebitCents ?? 0) - (p.CreditCents ?? 0);

            var parameters = new
            {
                sequence = @event.Sequence,
                account_id = p.AccountId,
                occurred_at = @event.OccurredAt,
                debit_cents = p.DebitCents,
                credit_cents = p.CreditCents,
                currency = p.Currency,
                delta,
                description
            };

            await conn.ExecuteAsync(new CommandDefinition(SqlQueries.InsertStatementLine, parameters, tx, cancellationToken: ct));
        }
    }

    private static (IReadOnlyList<PostingSnapshot>, string) Extract(TransactionPosted evt) => (evt.Postings, evt.Description);

    private static (IReadOnlyList<PostingSnapshot>, string) ExtractReversal(TransactionReversed evt) => (evt.Postings, $"Reversal: {evt.Reason}");
}
