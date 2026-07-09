namespace Ledger.Domain.Events;

public sealed record AccountOpened : DomainEvent
{
    public Guid AccountId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public AccountType Type { get; init; }
    public string Currency { get; init; } = string.Empty;
}