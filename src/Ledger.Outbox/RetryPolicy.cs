namespace Ledger.Outbox;

/// <summary>
/// Backoff exponencial para linhas do outbox que falharam ao publicar.
/// Funcao pura: sem relogio, sem I/O, sem aleatoriedade — o chamador soma o delay ao NOW() do banco.
/// </summary>
public static class RetryPolicy
{
    public const int MAX_ATTEMPTS = 8;

    private static readonly TimeSpan MAX_DELAY = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Delay ate a proxima tentativa, dado o numero de tentativas ja falhadas.
    /// 1 -> 1s, 2 -> 2s, 3 -> 4s, 4 -> 8s, 5 -> 16s, 6 -> 32s, 7 -> 60s (teto).
    /// </summary>
    public static TimeSpan ComputeDelay(int attempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);

        // ana: por que limitar o expoente antes do shift em vez de so clampar o resultado?
        var exponent = Math.Min(attempts - 1, 30);
        var seconds = 1L << exponent;

        return seconds >= MAX_DELAY.TotalSeconds ? MAX_DELAY : TimeSpan.FromSeconds(seconds);
    }

    /// <summary>Depois de <see cref="MAX_ATTEMPTS"/> falhas a linha para de ser reprocessada.</summary>
    public static bool ShouldMoveToDead(int attempts) => attempts >= MAX_ATTEMPTS;
}
