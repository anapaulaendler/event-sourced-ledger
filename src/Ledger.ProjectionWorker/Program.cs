using Ledger.EventStore;
using Ledger.EventStore.Interfaces;
using Ledger.ProjectionWorker;
using Ledger.Projections;
using Ledger.Projections.Interfaces;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(sp =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("ConnectionStrings:Postgres not configured");
    return NpgsqlDataSource.Create(connectionString);
});

builder.Services.AddScoped<IEventStore, PostgresEventStore>();
builder.Services.AddScoped<IProjector, BalanceProjector>();
builder.Services.AddScoped<IProjector, StatementProjector>();
builder.Services.AddScoped<ProjectionRunner>();

builder.Services.AddHostedService<PollingProjectionService>();

var host = builder.Build();
host.Run();
