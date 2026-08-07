using System.Threading.Channels;

namespace Ledger.OutboxDispatcher;

/// <summary>
/// Coalesce N notificacoes do Postgres em "tem trabalho": o dispatcher so precisa saber
/// que chegou algo, nao quantos. Channel bounded(1) + DropWrite descarta sinais redundantes.
/// </summary>
public sealed class OutboxWakeSignal
{
    private readonly Channel<byte> _channel = Channel.CreateBounded<byte>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false
    });

    public void Signal() => _channel.Writer.TryWrite(0);

    /// <returns>true se acordou por sinal; false se estourou o timeout.</returns>
    public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            await _channel.Reader.ReadAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }
}
