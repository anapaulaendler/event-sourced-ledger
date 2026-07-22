using Ledger.Domain;

namespace Ledger.Api.Contracts;

public sealed record OpenAccountRequest
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public AccountType Type { get; init; }
    public string Currency { get; init; } = string.Empty;
}
