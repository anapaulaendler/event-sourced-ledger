using System.Net;
using System.Text;
using Ledger.Idempotency;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Ledger.Idempotency.Tests;

public sealed class IdempotencyMiddlewareTests : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;
    private IHost _host = null!;
    private HttpClient _client = null!;
    private int _handlerCallCount;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("docker.io/library/postgres:16")
            .WithDatabase("ledger_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
        await _postgres.StartAsync();

        var dataSource = NpgsqlDataSource.Create(_postgres.GetConnectionString());
        var migrationSql = await File.ReadAllTextAsync(
            "../../../../../src/Ledger.Idempotency/Migrations/002_CreateIdempotencyTable.sql");
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(migrationSql, conn);
        await cmd.ExecuteNonQueryAsync();

        _host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton(dataSource);
                    services.AddTransient<IdempotencyMiddleware>();
                });
                web.Configure(app =>
                {
                    app.UseMiddleware<IdempotencyMiddleware>();
                    app.Run(async httpCtx =>
                    {
                        Interlocked.Increment(ref _handlerCallCount);
                        httpCtx.Response.StatusCode = 201;
                        await httpCtx.Response.WriteAsync("""{"ok":true}""");
                    });
                });
            })
            .StartAsync();

        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await _postgres.DisposeAsync();
    }

    private static StringContent Body(string json) =>
        new(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task Missing_Idempotency_Key_On_Post_Returns_400()
    {
        var response = await _client.PostAsync("/foo", Body("""{"a":1}"""));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Same_Key_Same_Body_Returns_Cached_Response()
    {
        _handlerCallCount = 0;

        var msg1 = new HttpRequestMessage(HttpMethod.Post, "/foo") { Content = Body("""{"a":1}""") };
        msg1.Headers.Add("Idempotency-Key", "k1");
        var msg2 = new HttpRequestMessage(HttpMethod.Post, "/foo") { Content = Body("""{"a":1}""") };
        msg2.Headers.Add("Idempotency-Key", "k1");

        var r1 = await _client.SendAsync(msg1);
        var r2 = await _client.SendAsync(msg2);

        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
        Assert.Equal(1, _handlerCallCount);
    }

    [Fact]
    public async Task Same_Key_Different_Body_Returns_422()
    {
        var msg1 = new HttpRequestMessage(HttpMethod.Post, "/foo") { Content = Body("""{"a":1}""") };
        msg1.Headers.Add("Idempotency-Key", "k2");
        var msg2 = new HttpRequestMessage(HttpMethod.Post, "/foo") { Content = Body("""{"a":2}""") };
        msg2.Headers.Add("Idempotency-Key", "k2");

        await _client.SendAsync(msg1);
        var r2 = await _client.SendAsync(msg2);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, r2.StatusCode);
    }

    [Fact]
    public async Task Concurrent_Same_Key_Only_One_Processed()
    {
        _handlerCallCount = 0;

        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            var msg = new HttpRequestMessage(HttpMethod.Post, "/foo") { Content = Body("""{"a":1}""") };
            msg.Headers.Add("Idempotency-Key", "k-concurrent");
            return await _client.SendAsync(msg);
        }).ToList();

        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal(1, _handlerCallCount);
    }

    [Fact]
    public async Task Get_Requests_Skip_Middleware()
    {
        var response = await _client.GetAsync("/foo");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
