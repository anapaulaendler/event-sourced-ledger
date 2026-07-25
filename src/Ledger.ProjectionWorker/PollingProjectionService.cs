namespace Ledger.ProjectionWorker;

public sealed class PollingProjectionService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly ProjectionRunner _runner;
    private readonly ILogger<PollingProjectionService> _logger;

    public PollingProjectionService(ProjectionRunner runner, ILogger<PollingProjectionService> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PollingProjectionService started (interval: {Interval})", PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var batchFull = await _runner.RunOnceAsync(stoppingToken);
                if (batchFull) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Projection cycle failed; will retry after interval");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
