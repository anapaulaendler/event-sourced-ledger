using Ledger.Api.Endpoints;
using Ledger.EventStore;
using Ledger.EventStore.Interfaces;
using Ledger.Idempotency;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(sp =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("ConnectionStrings:Postgres not configured");
    return NpgsqlDataSource.Create(connectionString);
});
builder.Services.AddScoped<IEventStore, PostgresEventStore>();
builder.Services.AddTransient<IdempotencyMiddleware>();

var app = builder.Build();

app.UseMiddleware<Ledger.Api.GlobalExceptionMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAccountsEndpoints();
app.MapTransactionsEndpoints();
app.MapAuditEndpoints();
app.MapProjectionsEndpoints();

app.Run();

public partial class Program;
