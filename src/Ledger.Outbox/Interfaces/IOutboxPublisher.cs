namespace Ledger.Outbox.Interfaces;

public interface IOutboxPublisher
{
    /// <summary>
    /// Publica uma linha do outbox. So retorna depois do ack do broker — o chamador
    /// so pode marcar 'sent' quando esta Task completar sem excecao.
    /// </summary>
    Task PublishAsync(OutboxRecord record, CancellationToken ct);
}
