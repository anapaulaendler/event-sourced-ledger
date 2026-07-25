using Ledger.EventStore;
using Ledger.EventStore.Interfaces;
using Ledger.ProjectionWorker;
using Ledger.Projections;
using Ledger.Projections.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Ledger.Projections.Tests;

public sealed class ProjectionsFixture : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;

    public NpgsqlDataSource DataSource { get; private set; } = null!;
    public IEventStore Store { get; private set; } = null!;
    public ProjectionRunner Runner { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("docker.io/library/postgres:16")
            .WithDatabase("ledger_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
        await _postgres.StartAsync();

        DataSource = NpgsqlDataSource.Create(_postgres.GetConnectionString());

        await using var conn = await DataSource.OpenConnectionAsync();
        foreach (var path in new[]
        {
            "../../../../../src/Ledger.EventStore/Migrations/001_CreateEventsTable.sql",
            "../../../../../src/Ledger.Projections/Migrations/003_CreateBalancesTable.sql",
            "../../../../../src/Ledger.Projections/Migrations/004_CreateStatementTable.sql",
            "../../../../../src/Ledger.Projections/Migrations/005_CreateCheckpointsTable.sql"
        })
        {
            var sql = await File.ReadAllTextAsync(path);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        Store = new PostgresEventStore(DataSource);
        var projectors = new IProjector[] { new BalanceProjector(), new StatementProjector() };
        Runner = new ProjectionRunner(DataSource, Store, projectors, NullLogger<ProjectionRunner>.Instance);
    }

    public async Task DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
