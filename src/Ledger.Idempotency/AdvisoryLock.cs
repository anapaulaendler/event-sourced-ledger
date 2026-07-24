using Dapper;
using Npgsql;

namespace Ledger.Idempotency;

public static class AdvisoryLock
{
    private const string Sql = "SELECT pg_advisory_xact_lock(hashtext(@key)::bigint)";

    public static async Task AcquireForKeyAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, string key, CancellationToken ct)
    {
        await conn.ExecuteAsync(new CommandDefinition(Sql, new { key }, tx, cancellationToken: ct));
    }
}
