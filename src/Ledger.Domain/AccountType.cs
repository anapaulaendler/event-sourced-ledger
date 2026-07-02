namespace Ledger.Domain;

public enum AccountType : byte 
{
    Asset = 0, 
    Liability = 1, 
    Equity = 2, 
    Revenue = 3, 
    Expense = 4
}