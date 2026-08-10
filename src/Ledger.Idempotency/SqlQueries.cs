namespace Ledger.Idempotency;

internal static class SqlQueries
{
    // Aliases explicitos de proposito: sem eles o mapeamento depende de
    // DefaultTypeMap.MatchNamesWithUnderscores, estado global de processo setado pelo ctor
    // estatico do PostgresEventStore. Quem usa esta lib sem instanciar um EventStore
    // materializava RequestHash vazio -> 422 em requisicao valida. Nao remover.
    public const string TryGet = """
        SELECT key,
               request_hash    AS requesthash,
               response_status AS responsestatus,
               response_body::text AS responsebody,
               expires_at      AS expiresat
        FROM idempotency_keys
        WHERE key = @key AND expires_at > NOW()
        """;

    public const string Insert = """
        INSERT INTO idempotency_keys (key, request_hash, response_status, response_body, expires_at)
        VALUES (@key, @request_hash, @response_status, @response_body::jsonb, @expires_at)
        """;
}
