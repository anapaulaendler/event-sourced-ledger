namespace Ledger.Outbox;

internal static class SqlQueries
{
    // ana: por que "next_attempt_at <= NOW()" e nao so "state = 'pending'"?
    // ana: o que FOR UPDATE SKIP LOCKED da que um advisory lock nao daria?
    public const string FetchPending = """
        SELECT sequence, aggregate_id, envelope::text AS envelope, attempts
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
