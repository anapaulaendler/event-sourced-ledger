using Npgsql;

namespace Ledger.OutboxDispatcher;

public sealed class NotifyListenerService(
    NpgsqlDataSource dataSource,
    OutboxWakeSignal wake,
    ILogger<NotifyListenerService> logger) : BackgroundService
{
    public const string CHANNEL_NAME = "outbox_pending";

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
                logger.LogError(ex, "LISTEN loop crashed; reconnecting in 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        // ana: por que esta conexao fica fora do pool o tempo todo?
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        conn.Notification += (_, _) => wake.Signal();

        await using (var cmd = new NpgsqlCommand($"LISTEN {CHANNEL_NAME}", conn))
        {
            await cmd.ExecuteNonQueryAsync(ct);
        }

        logger.LogInformation("Listening on channel {Channel}", CHANNEL_NAME);

        while (!ct.IsCancellationRequested)
        {
            await conn.WaitAsync(ct);
        }
    }
}
