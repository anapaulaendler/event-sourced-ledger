using Dapper;
using Npgsql;

namespace Ledger.Outbox;

public static class OutboxStore
{
    /// <summary>
    /// Le um lote de linhas pendentes e as trava ate o fim da transacao.
    /// O lease dura enquanto <paramref name="tx"/> estiver aberta — quem chama
    /// e responsavel por commitar/abortar.
    /// </summary>
    public static async Task<IReadOnlyList<OutboxRecord>> FetchPendingAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, int batchSize, CancellationToken ct)
    {
        var rows = await conn.QueryAsync<OutboxRecord>(
            new CommandDefinition(SqlQueries.FetchPending, new { batch_size = batchSize }, tx, cancellationToken: ct));

        return rows.AsList();
    }

    public static async Task MarkSentAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, long sequence, CancellationToken ct)
    {
        await conn.ExecuteAsync(
            new CommandDefinition(SqlQueries.MarkSent, new { sequence }, tx, cancellationToken: ct));
    }
}
