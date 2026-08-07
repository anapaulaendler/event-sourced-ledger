namespace Ledger.OutboxDispatcher;

public sealed class OutboxDispatcherService(
    OutboxDispatcher dispatcher,
    ILogger<OutboxDispatcherService> logger) : BackgroundService
{
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

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
