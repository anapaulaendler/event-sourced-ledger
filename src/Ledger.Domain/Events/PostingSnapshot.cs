namespace Ledger.Domain.Events;

public sealed record PostingSnapshot(Guid AccountId, long? DebitCents, long? CreditCents, string Currency);