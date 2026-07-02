namespace Ledger.Domain.Exceptions;

public sealed class CurrencyMismatchException : InvalidOperationException
{
    public CurrencyMismatchException(string currency1, string currency2)
        : base($"Cannot operate between {currency1} and {currency2}") { }
}
