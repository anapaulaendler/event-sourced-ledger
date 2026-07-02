namespace Ledger.Domain;

public sealed record Account
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public AccountType Type { get; init; }
    public string Currency { get; init; } = "BRL";
}