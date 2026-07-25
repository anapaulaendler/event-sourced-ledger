using Dapper;
using Npgsql;

namespace Ledger.Api.Endpoints;

public static class ProjectionsEndpoints
{
    private const string GetBalanceSql = """
        SELECT balance_cents AS balanceCents, last_sequence AS lastSequence, updated_at AS updatedAt
        FROM balances WHERE account_id = @accountId AND currency = @currency
        """;

    private const string GetStatementSql = """
        SELECT sequence, account_id AS accountId, occurred_at AS occurredAt,
               debit_cents AS debitCents, credit_cents AS creditCents,
               currency, running_balance_cents AS runningBalanceCents, description
        FROM statement
        WHERE account_id = @accountId
          AND (@from::timestamptz IS NULL OR occurred_at >= @from)
          AND (@to::timestamptz IS NULL OR occurred_at <= @to)
        ORDER BY sequence
        LIMIT @limit
        """;

    private const string RebuildSql = """
        TRUNCATE balances;
        TRUNCATE statement;
        DELETE FROM projection_checkpoints;
        SELECT pg_notify('events_appended', '0');
        """;

    public static void MapProjectionsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/balance", async (Guid accountId, string currency, NpgsqlDataSource ds, CancellationToken ct) =>
        {
            await using var conn = await ds.OpenConnectionAsync(ct);
            var row = await conn.QuerySingleOrDefaultAsync(new CommandDefinition(GetBalanceSql, new { accountId, currency }, cancellationToken: ct));
            if (row is null) return Results.Ok(new { accountId, currency, balanceCents = 0L, lastSequence = 0L });
            return Results.Ok(new { accountId, currency, balanceCents = (long)row.balanceCents, lastSequence = (long)row.lastSequence, updatedAt = (DateTime)row.updatedAt });
        });

        app.MapGet("/statement", async (Guid accountId, DateTimeOffset? from, DateTimeOffset? to, int? limit, NpgsqlDataSource ds, CancellationToken ct) =>
        {
            await using var conn = await ds.OpenConnectionAsync(ct);
            var rows = await conn.QueryAsync(new CommandDefinition(GetStatementSql,
                new { accountId, from = from?.UtcDateTime, to = to?.UtcDateTime, limit = Math.Min(limit ?? 100, 500) },
                cancellationToken: ct));
            return Results.Ok(new { accountId, lines = rows });
        });

        app.MapPost("/admin/projections/rebuild", async (NpgsqlDataSource ds, CancellationToken ct) =>
        {
            await using var conn = await ds.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(RebuildSql, cancellationToken: ct));
            return Results.Accepted(value: new { status = "rebuild triggered" });
        });
    }
}
