namespace Ledger.OutboxDispatcher;

public sealed class OutboxDispatcherService(
    OutboxDispatcher dispatcher,
    OutboxWakeSignal wake,
    ILogger<OutboxDispatcherService> logger) : BackgroundService
{
    /// <summary>Rede de seguranca: se o LISTEN cair, o despacho continua neste ritmo.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxDispatcherService started (interval: {Interval})", PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var batchFull = await dispatcher.RunOnceAsync(stoppingToken);
                if (batchFull) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Dispatch cycle failed; will retry after interval");
            }

            await wake.WaitAsync(PollInterval, stoppingToken);
        }
    }
}
