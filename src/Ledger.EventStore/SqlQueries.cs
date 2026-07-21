namespace Ledger.EventStore;

internal static class SqlQueries
{
    public const string GetMaxVersion = """
        SELECT COALESCE(MAX(version), -1)
        FROM events
        WHERE stream_id = @stream_id
        """;

    public const string InsertEvent = """
        INSERT INTO events (stream_id, version, type, payload, occurred_at)
        VALUES (@stream_id, @version, @type, @payload::jsonb, @occurred_at)
        """;

    public const string ReadStream = """
        SELECT sequence, stream_id, version, type, payload::text AS payload_json, occurred_at, correlation_id
        FROM events
        WHERE stream_id = @stream_id
        ORDER BY version
        """;

    public const string ReadAll = """
        SELECT sequence, stream_id, version, type, payload::text AS payload_json, occurred_at, correlation_id
        FROM events
        WHERE sequence > @from_sequence
        ORDER BY sequence
        LIMIT @max
        """;
}
