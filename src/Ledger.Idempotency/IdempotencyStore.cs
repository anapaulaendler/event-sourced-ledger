using Dapper;
using Npgsql;

namespace Ledger.Idempotency;

public static class IdempotencyStore
{
    public static async Task<IdempotencyRecord?> TryGetAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string key, CancellationToken ct)
    {
        return await conn.QuerySingleOrDefaultAsync<IdempotencyRecord>(new CommandDefinition(SqlQueries.TryGet, new { key }, tx, cancellationToken: ct));
    }

    public static async Task InsertAsync(NpgsqlConnection conn, NpgsqlTransaction tx, IdempotencyRecord record, CancellationToken ct)
    {
        var parameters = new
        {
            key = record.Key,
            request_hash = record.RequestHash,
            response_status = record.ResponseStatus,
            response_body = record.ResponseBody,
            expires_at = record.ExpiresAt
        };
        await conn.ExecuteAsync(new CommandDefinition(SqlQueries.Insert, parameters, tx, cancellationToken: ct));
    }
}
