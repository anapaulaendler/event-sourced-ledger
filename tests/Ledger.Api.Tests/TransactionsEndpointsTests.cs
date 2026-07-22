using System.Net;
using System.Net.Http.Json;
using Ledger.Api.Contracts;

namespace Ledger.Api.Tests;

public sealed class TransactionsEndpointsTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;
    public TransactionsEndpointsTests(ApiTestFixture fx) => _client = fx.CreateClient();

    [Fact]
    public async Task Post_Transactions_Balanced_Returns_201()
    {
        var accountA = Guid.NewGuid();
        var accountB = Guid.NewGuid();

        var req = new HttpRequestMessage(HttpMethod.Post, "/transactions")
        {
            Content = JsonContent.Create(new PostTransactionRequest
            {
                OccurredAt = DateTimeOffset.UtcNow,
                Description = "test",
                Postings = new[]
                {
                    new PostingRequest { AccountId = accountA, Debit = new MoneyRequest { AmountCents = 10000, Currency = "BRL" } },
                    new PostingRequest { AccountId = accountB, Credit = new MoneyRequest { AmountCents = 10000, Currency = "BRL" } }
                }
            })
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Post_Transactions_Unbalanced_Returns_400()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/transactions")
        {
            Content = JsonContent.Create(new PostTransactionRequest
            {
                OccurredAt = DateTimeOffset.UtcNow,
                Description = "unbalanced",
                Postings = new[]
                {
                    new PostingRequest { AccountId = Guid.NewGuid(), Debit = new MoneyRequest { AmountCents = 10000, Currency = "BRL" } },
                    new PostingRequest { AccountId = Guid.NewGuid(), Credit = new MoneyRequest { AmountCents = 5000, Currency = "BRL" } }
                }
            })
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
