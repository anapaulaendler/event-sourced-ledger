namespace Ledger.Idempotency;

public sealed record IdempotencyRecord
{
    public string Key { get; init; } = string.Empty;
    public string RequestHash { get; init; } = string.Empty;
    public int ResponseStatus { get; init; }
    public string ResponseBody { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}
