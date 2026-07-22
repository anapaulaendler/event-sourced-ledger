using Ledger.Api.Contracts;
using Ledger.Domain.Events;
using Ledger.EventStore.Interfaces;

namespace Ledger.Api.Endpoints;

public static class AccountsEndpoints
{
    public static void MapAccountsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/accounts", async (OpenAccountRequest req, IEventStore store, CancellationToken ct) =>
        {
            var accountId = Guid.NewGuid();
            var evt = new AccountOpened
            {
                AccountId = accountId,
                Code = req.Code,
                Name = req.Name,
                Type = req.Type,
                Currency = req.Currency,
                OccurredAt = DateTime.UtcNow
            };

            await store.AppendAsync(accountId, expectedVersion: -1, [evt], ct);

            return Results.Created($"/accounts/{accountId}", new
            {
                accountId,
                req.Code,
                req.Name,
                req.Type,
                req.Currency
            });
        });
    }
}
