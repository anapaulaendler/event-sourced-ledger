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

    /// <summary>Erros longos (stack trace inteiro) por milhoes de linhas viram bloat; guardamos so o inicio.</summary>
    public const int MAX_ERROR_LENGTH = 2000;

    /// <summary>
    /// Contabiliza uma falha de publicacao: incrementa attempts e reagenda via <see cref="RetryPolicy"/>,
    /// ou move a linha para dead-letter quando o orcamento de tentativas acaba.
    /// </summary>
    /// <returns>true se a linha foi para dead-letter.</returns>
    public static async Task<bool> MarkFailedAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, OutboxRecord record, string error, CancellationToken ct)
    {
        var attempts = record.Attempts + 1;
        var movedToDead = RetryPolicy.ShouldMoveToDead(attempts);

        var parameters = new
        {
            sequence = record.Sequence,
            attempts,
            state = movedToDead ? "dead" : "pending",
            last_error = Truncate(error),
            delay = movedToDead ? TimeSpan.Zero : RetryPolicy.ComputeDelay(attempts)
        };

        await conn.ExecuteAsync(new CommandDefinition(SqlQueries.MarkFailed, parameters, tx, cancellationToken: ct));

        return movedToDead;
    }

    private static string Truncate(string error) =>
        error.Length <= MAX_ERROR_LENGTH ? error : error[..MAX_ERROR_LENGTH];
}
