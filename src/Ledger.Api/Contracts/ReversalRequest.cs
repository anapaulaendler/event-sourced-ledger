namespace Ledger.Api.Contracts;

public sealed record ReversalRequest
{
    public string Reason { get; init; } = string.Empty;
}
