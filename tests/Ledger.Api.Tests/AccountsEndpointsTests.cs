using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ledger.Api.Contracts;
using Ledger.Domain;

namespace Ledger.Api.Tests;

public sealed class AccountsEndpointsTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;

    public AccountsEndpointsTests(ApiTestFixture fx)
    {
        _client = fx.CreateClient();
    }

    [Fact]
    public async Task Post_Accounts_Creates_Account_And_Emits_Event()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/accounts")
        {
            Content = JsonContent.Create(new OpenAccountRequest
            {
                Code = "1.1.01",
                Name = "Caixa",
                Type = AccountType.Asset,
                Currency = "BRL"
            })
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.NotEqual(Guid.Empty, doc.RootElement.GetProperty("accountId").GetGuid());
    }

    [Fact]
    public async Task Post_Accounts_Without_Idempotency_Key_Returns_400()
    {
        var response = await _client.PostAsJsonAsync("/accounts", new OpenAccountRequest
        {
            Code = "1.1.02",
            Name = "Conta X",
            Type = AccountType.Asset,
            Currency = "BRL"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
