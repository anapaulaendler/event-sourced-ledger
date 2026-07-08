using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Ledger.Domain.Tests;

public class TransactionPropertyTests
{
    public static Gen<Money> GenMoney =>
        from amount in Gen.Choose(1, 100_000_000)
        from currency in Gen.Elements("BRL", "USD", "EUR")
        select new Money(amount, currency);

    public static Gen<Transaction> GenBalancedTransaction =>
        from money in GenMoney
        select new Transaction(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "generated",
            [
                Posting.FromDebit(Guid.NewGuid(), money),
                Posting.FromCredit(Guid.NewGuid(), money)
            ]);

    [Property(MaxTest = 1000)]
    public Property Balanced_Transactions_Have_Equal_Sums_Per_Currency()
    {
        return Prop.ForAll(GenBalancedTransaction.ToArbitrary(), tx =>
        {
            var byCurrency = tx.Postings.GroupBy(p => p.Amount.Currency);

            foreach (var group in byCurrency)
            {
                var debits = group.Where(p => p.Debit is not null).Sum(p => p.Debit!.AmountCents);
                var credits = group.Where(p => p.Credit is not null).Sum(p => p.Credit!.AmountCents);

                if (debits != credits) return false;
            }
            return true;
        });
    }

    [Property(MaxTest = 1000)]
    public Property Money_Addition_Is_Commutative()
    {
        return Prop.ForAll(GenMoney.ToArbitrary(), GenMoney.ToArbitrary(), (m1, m2) =>
        {
            if (m1.Currency != m2.Currency) return true;

            return (m1 + m2).Equals(m2 + m1);
        });
    }

    [Property(MaxTest = 1000)]
    public Property Adding_Zero_Preserves_Value()
    {
        return Prop.ForAll(GenMoney.ToArbitrary(), m =>
        {
            return m + Money.Zero(m.Currency) == m;
        });
    }
}
