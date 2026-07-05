using Ledger.Domain.Exceptions;

namespace Ledger.Domain.Tests;

public class TransactionTests
{
    private static Guid AccountA => Guid.NewGuid();
    private static Guid AccountB => Guid.NewGuid();

    [Fact]
    public void Balanced_Transaction_Is_Valid()
    {
        var d = Posting.FromDebit(AccountA, new Money(100, "BRL"));
        var c = Posting.FromCredit(AccountB, new Money(100, "BRL"));

        var tx = new Transaction(Guid.NewGuid(), DateTimeOffset.UtcNow, "Balanced_Transaction_Is_Valid", [d, c]);

        Assert.Equal(2, tx.Postings.Count);
    }

    [Fact]
    public void Unbalanced_Transaction_Throws()
    {
        var d = Posting.FromDebit(AccountA, new Money(100, "BRL"));
        var c = Posting.FromCredit(AccountB, new Money(50, "BRL"));

        Assert.Throws<UnbalancedTransactionException>(() => 
            new Transaction(Guid.NewGuid(), DateTimeOffset.UtcNow, "Unbalanced_Transaction_Throws", [d, c]));
    }

    [Fact]
    public void Transaction_Without_Postings_Throws()
    {
        Assert.Throws<ArgumentException>(() => 
            new Transaction(Guid.NewGuid(), DateTimeOffset.UtcNow, "Transaction_Without_Postings_Throws", []));
    }

    [Fact]
    public void Transaction_With_Balanced_Multi_Currency_Is_Valid()
    {
        var dBRL = Posting.FromDebit(AccountA, new Money(100, "BRL"));
        var dUSD = Posting.FromDebit(AccountA, new Money(50, "USD"));
        var cBRL = Posting.FromCredit(AccountB, new Money(100, "BRL"));
        var cUSD = Posting.FromCredit(AccountB, new Money(50, "USD"));

        var tx = new Transaction(Guid.NewGuid(), DateTimeOffset.UtcNow, "Balanced_Transaction_Is_Valid", [dBRL, dUSD, cBRL, cUSD]);

        Assert.Equal(4, tx.Postings.Count);
    }

    [Fact]
    public void Transaction_With_Unbalanced_Multi_Currency_Throws()
    {
        var dBRL = Posting.FromDebit(AccountA, new Money(100, "BRL"));
        var dUSD = Posting.FromDebit(AccountA, new Money(50, "USD"));
        var cBRL = Posting.FromCredit(AccountB, new Money(100, "BRL"));
        var cUSD = Posting.FromCredit(AccountB, new Money(100, "USD"));

        Assert.Throws<UnbalancedTransactionException>(() => 
            new Transaction(Guid.NewGuid(), DateTimeOffset.UtcNow, "Transaction_With_Unbalanced_Multi_Currency_Throws", [dBRL, dUSD, cBRL, cUSD]));
    }

    [Fact]
    public void Balanced_Transaction_With_Multiple_Postings_Same_Side_Is_Valid()
    {
        var d = Posting.FromDebit(AccountA, new Money(100, "BRL"));
        var cB = Posting.FromCredit(AccountB, new Money(30, "BRL"));
        var cC = Posting.FromCredit(Guid.NewGuid(), new Money(70, "BRL"));

        var tx = new Transaction(Guid.NewGuid(), DateTimeOffset.UtcNow, "Balanced_Transaction_With_Multiple_Postings_Same_Side_Is_Valid", [d, cB, cC]);

        Assert.Equal(3, tx.Postings.Count);
    }

    [Fact]
    public void Transaction_With_All_Postings_Same_Side_Throws()
    {
        var d1 = Posting.FromDebit(AccountA, new Money(100, "BRL"));
        var d2 = Posting.FromDebit(AccountA, new Money(100, "BRL"));
        var d3 = Posting.FromDebit(AccountA, new Money(100, "BRL"));

        Assert.Throws<UnbalancedTransactionException>(() => 
            new Transaction(Guid.NewGuid(), DateTimeOffset.UtcNow, "Transaction_With_All_Postings_Same_Side_Throws", [d1, d2, d3]));
    }
}
