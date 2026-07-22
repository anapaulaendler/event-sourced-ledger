using System.Net;
using System.Net.Http.Json;
using Ledger.Api.Contracts;
using Ledger.Domain;

namespace Ledger.Api.Tests;

public sealed class AuditEndpointsTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;
    public AuditEndpointsTests(ApiTestFixture fx) => _client = fx.CreateClient();

    [Fact]
    public async Task Get_Audit_Events_Returns_Events_After_Post()
    {
        var openReq = new HttpRequestMessage(HttpMethod.Post, "/accounts")
        {
            Content = JsonContent.Create(new OpenAccountRequest
            { Code = "9.9.99", Name = "Audit", Type = AccountType.Asset, Currency = "BRL" })
        };
        openReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        await _client.SendAsync(openReq);

        var response = await _client.GetAsync("/audit/events");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("AccountOpened", body);
    }
}
