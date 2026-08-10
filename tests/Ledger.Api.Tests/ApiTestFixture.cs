using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Ledger.Api.Tests;

public sealed class ApiTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("docker.io/library/postgres:16")
            .WithDatabase("ledger_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
        await _postgres.StartAsync();

        var dataSource = NpgsqlDataSource.Create(ConnectionString);
        await using var conn = await dataSource.OpenConnectionAsync();

        foreach (var path in new[]
        {
            "../../../../../src/Ledger.EventStore/Migrations/001_CreateEventsTable.sql",
            "../../../../../src/Ledger.Idempotency/Migrations/002_CreateIdempotencyTable.sql",
            "../../../../../src/Ledger.Projections/Migrations/003_CreateBalancesTable.sql",
            "../../../../../src/Ledger.Projections/Migrations/004_CreateStatementTable.sql",
            "../../../../../src/Ledger.Projections/Migrations/005_CreateCheckpointsTable.sql",
            "../../../../../src/Ledger.EventStore/Migrations/006_CreateEventsNotifyTrigger.sql"
        })
        {
            var sql = await File.ReadAllTextAsync(path);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = ConnectionString
            });
        });
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
