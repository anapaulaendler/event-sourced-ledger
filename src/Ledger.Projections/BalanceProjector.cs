using System.Text.Json;
using Dapper;
using Ledger.Domain.Events;
using Ledger.EventStore.Events;
using Ledger.Projections.Interfaces;
using Npgsql;

namespace Ledger.Projections;

public sealed class BalanceProjector : IProjector
{
    public string Name => "balances";

    public async Task ApplyAsync(StoredEvent @event, NpgsqlConnection conn, NpgsqlTransaction tx, CancellationToken ct)
    {
        var postings = @event.Type switch
        {
            nameof(TransactionPosted) => JsonSerializer.Deserialize<TransactionPosted>(@event.PayloadJson)!.Postings,
            nameof(TransactionReversed) => JsonSerializer.Deserialize<TransactionReversed>(@event.PayloadJson)!.Postings,
            _ => null
        };

        if (postings is null) return;

        foreach (var p in postings)
        {
            var delta = (p.DebitCents ?? 0) - (p.CreditCents ?? 0);

            var parameters = new
            {
                account_id = p.AccountId,
                currency = p.Currency,
                delta,
                sequence = @event.Sequence
            };

            await conn.ExecuteAsync(new CommandDefinition(SqlQueries.UpsertBalance, parameters, tx, cancellationToken: ct));
        }
    }
}
