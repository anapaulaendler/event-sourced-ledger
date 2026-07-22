namespace Ledger.Idempotency;

internal static class SqlQueries
{
    public const string TryGet = """
        SELECT key, request_hash, response_status, response_body::text AS response_body, expires_at
        FROM idempotency_keys
        WHERE key = @key AND expires_at > NOW()
        """;

    public const string Insert = """
        INSERT INTO idempotency_keys (key, request_hash, response_status, response_body, expires_at)
        VALUES (@key, @request_hash, @response_status, @response_body::jsonb, @expires_at)
        """;
}
