using System.Net;
using System.Net.Http.Json;

namespace Ledger.Api.Tests;

public class ProjectionsEndpointsTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _client;

    public ProjectionsEndpointsTests(ApiTestFixture fixture) => _client = fixture.CreateClient();

    [Fact]
    public async Task Get_Balance_Unknown_Account_Returns_Zero()
    {
        var accountId = Guid.NewGuid();
        var response = await _client.GetAsync($"/balance?accountId={accountId}&currency=BRL");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal(0L, ((System.Text.Json.JsonElement)body!["balanceCents"]).GetInt64());
    }

    [Fact]
    public async Task Get_Statement_Unknown_Account_Returns_Empty_Lines()
    {
        var accountId = Guid.NewGuid();
        var response = await _client.GetAsync($"/statement?accountId={accountId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"lines\":[]", body);
    }

    [Fact]
    public async Task Post_Admin_Rebuild_Returns_202()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/admin/projections/rebuild");
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
