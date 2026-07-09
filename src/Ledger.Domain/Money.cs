using Ledger.Domain.Exceptions;

namespace Ledger.Domain;

public sealed record Money
{
    public long AmountCents { get; }
    public string Currency { get; }

    public Money(long amountCents, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be ISO 4217 3-letter code", nameof(currency));

        AmountCents = amountCents;
        Currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency) => new(0, currency);

    public Money Add(Money other)
    {
        if (other.Currency != Currency)
            throw new CurrencyMismatchException(Currency, other.Currency);

        return new Money(AmountCents + other.AmountCents, Currency);
    }

    public Money Subtract(Money other) => Add(new Money(-other.AmountCents, other.Currency));

    public static Money operator +(Money a, Money b) => a.Add(b);
    public static Money operator -(Money a, Money b) => a.Subtract(b);
}
