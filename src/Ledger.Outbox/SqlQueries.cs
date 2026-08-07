namespace Ledger.Outbox;

internal static class SqlQueries
{
    // ana: por que "next_attempt_at <= NOW()" e nao so "state = 'pending'"?
    // ana: o que FOR UPDATE SKIP LOCKED da que um advisory lock nao daria?
    // Alias explicito de proposito: sem ele o mapeamento depende de
    // DefaultTypeMap.MatchNamesWithUnderscores, que e estado global de processo setado pelo
    // ctor estatico do PostgresEventStore. O dispatcher nunca instancia um, entao a flag
    // ficava false e aggregate_id virava Guid.Empty -> key errada no Kafka. Nao remover.
    public const string FetchPending = """
        SELECT sequence, aggregate_id AS aggregateid, envelope::text AS envelope, attempts
        FROM outbox
        WHERE state = 'pending' AND next_attempt_at <= NOW()
        ORDER BY sequence
        LIMIT @batch_size
        FOR UPDATE SKIP LOCKED
        """;

    public const string MarkSent = """
        UPDATE outbox
        SET state = 'sent',
            published_at = NOW(),
            last_error = NULL
        WHERE sequence = @sequence
        """;

    // ana: por que NOW() + @delay no banco em vez de mandar o timestamp ja calculado pelo dispatcher?
    public const string MarkFailed = """
        UPDATE outbox
        SET attempts = @attempts,
            state = @state,
            last_error = @last_error,
            next_attempt_at = NOW() + @delay
        WHERE sequence = @sequence
        """;
}
