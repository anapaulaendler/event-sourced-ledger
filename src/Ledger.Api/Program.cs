using Ledger.Api.Endpoints;
using Ledger.EventStore;
using Ledger.EventStore.Interfaces;
using Ledger.Idempotency;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres not configured");

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddScoped<IEventStore, PostgresEventStore>();
builder.Services.AddTransient<IdempotencyMiddleware>();

var app = builder.Build();

app.UseMiddleware<Ledger.Api.GlobalExceptionMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAccountsEndpoints();
app.MapTransactionsEndpoints();

app.Run();

public partial class Program;
