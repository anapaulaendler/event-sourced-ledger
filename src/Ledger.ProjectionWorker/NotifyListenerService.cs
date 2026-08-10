using Npgsql;

namespace Ledger.ProjectionWorker;

public sealed class NotifyListenerService : BackgroundService
{
    private const string CHANNEL_NAME = "events_appended";

    private readonly NpgsqlDataSource _dataSource;
    private readonly ProjectionWakeSignal _wake;
    private readonly ILogger<NotifyListenerService> _logger;

    public NotifyListenerService(NpgsqlDataSource dataSource, ProjectionWakeSignal wake, ILogger<NotifyListenerService> logger)
    {
        _dataSource = dataSource;
        _wake = wake;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ListenLoopAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LISTEN loop crashed; reconnecting in 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        conn.Notification += (_, _) => _wake.Signal();

        await using (var cmd = new NpgsqlCommand($"LISTEN {CHANNEL_NAME}", conn))
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }

        _logger.LogInformation("Listening on channel {Channel}", CHANNEL_NAME);

        while (!ct.IsCancellationRequested)
        {
            await conn.WaitAsync(ct);
        }
    }
}
