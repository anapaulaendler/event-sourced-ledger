namespace Ledger.Domain.Exceptions;

public sealed class UnbalancedTransactionException : InvalidOperationException
{
    public UnbalancedTransactionException(string currency, long sumDebits, long sumCredits)
        : base($"Transaction unbalanced in {currency}: debits={sumDebits} credits={sumCredits}") { }
}
