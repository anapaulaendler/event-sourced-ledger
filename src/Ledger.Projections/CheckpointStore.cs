using Dapper;
using Npgsql;

namespace Ledger.Projections;

public static class CheckpointStore
{
    private const string GetSql = "SELECT last_sequence FROM projection_checkpoints WHERE projector_name = @name";

    private const string UpsertSql = """
        INSERT INTO projection_checkpoints (projector_name, last_sequence, updated_at)
        VALUES (@name, @sequence, NOW())
        ON CONFLICT (projector_name) DO UPDATE
        SET last_sequence = EXCLUDED.last_sequence,
            updated_at = NOW()
        WHERE projection_checkpoints.last_sequence < EXCLUDED.last_sequence
        """;

    public static async Task<long> GetAsync(NpgsqlConnection conn, NpgsqlTransaction? tx, string projectorName, CancellationToken ct)
    {
        var value = await conn.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(GetSql, new { name = projectorName }, tx, cancellationToken: ct));
        return value ?? 0L;
    }

    public static async Task SetAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string projectorName, long sequence, CancellationToken ct)
    {
        await conn.ExecuteAsync(new CommandDefinition(UpsertSql, new { name = projectorName, sequence }, tx, cancellationToken: ct));
    }
}
