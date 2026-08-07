using Ledger.Outbox;
using Ledger.Outbox.Interfaces;
using Npgsql;

namespace Ledger.OutboxDispatcher;

/// <summary>
/// Um ciclo de despacho: reivindica um lote pendente, publica tudo em paralelo e
/// resolve linha por linha (sent ou failed) na mesma transacao que segurou o lease.
/// </summary>
public sealed class OutboxDispatcher(
    NpgsqlDataSource dataSource,
    IOutboxPublisher publisher,
    ILogger<OutboxDispatcher> logger)
{
    public const int BATCH_SIZE = 50;

    /// <returns>true se o lote veio cheio — ha mais trabalho, nao vale esperar o poll.</returns>
    public async Task<bool> RunOnceAsync(CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var records = await OutboxStore.FetchPendingAsync(conn, tx, BATCH_SIZE, ct);
        if (records.Count == 0)
        {
            await tx.CommitAsync(ct);
            return false;
        }

        // ana: por que disparar todos os ProduceAsync antes de dar await em qualquer um?
        var inFlight = records
            .Select(record => (record, publish: publisher.PublishAsync(record, ct)))
            .ToList();

        try
        {
            await Task.WhenAll(inFlight.Select(item => item.publish));
        }
        catch
        {
            // Falhas individuais sao tratadas abaixo; o WhenAll so serve para esperar todas.
        }

        if (ct.IsCancellationRequested)
        {
            await tx.RollbackAsync(CancellationToken.None);
            return false;
        }

        var sent = 0;
        var failed = 0;
        var dead = 0;

        foreach (var (record, publish) in inFlight)
        {
            if (publish.IsCompletedSuccessfully)
            {
                await OutboxStore.MarkSentAsync(conn, tx, record.Sequence, ct);
                sent++;
                continue;
            }

            var error = publish.Exception?.GetBaseException().Message ?? "publish cancelado";
            if (await OutboxStore.MarkFailedAsync(conn, tx, record, error, ct))
            {
                dead++;
                logger.LogError("Outbox {Sequence} esgotou {MaxAttempts} tentativas e foi para dead-letter: {Error}",
                    record.Sequence, RetryPolicy.MAX_ATTEMPTS, error);
            }
            else
            {
                failed++;
            }
        }

        await tx.CommitAsync(ct);

        if (failed > 0 || dead > 0)
        {
            logger.LogWarning("Ciclo de outbox: {Sent} enviadas, {Failed} reagendadas, {Dead} em dead-letter", sent, failed, dead);
        }
        else
        {
            logger.LogInformation("Ciclo de outbox: {Sent} enviadas", sent);
        }

        return records.Count == BATCH_SIZE;
    }
}
