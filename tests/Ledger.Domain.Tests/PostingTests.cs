using Ledger.Domain;

namespace Ledger.Domain.Tests;

public class PostingTests
{
    private readonly Guid _accountId = Guid.NewGuid();

    [Fact]
    public void Posting_With_Only_Debit_Is_Valid()
    {
        var p = Posting.FromDebit(_accountId, new Money(100, "BRL"));

        Assert.Equal(_accountId, p.AccountId);
        Assert.NotNull(p.Debit);
        Assert.Null(p.Credit);
    }

    [Fact]
    public void Posting_With_Only_Credit_Is_Valid()
    {
        var p = Posting.FromCredit(_accountId, new Money(100, "BRL"));

        Assert.Null(p.Debit);
        Assert.NotNull(p.Credit);
    }

    [Fact]
    public void Posting_With_Both_Debit_And_Credit_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new Posting(_accountId, new Money(100, "BRL"), new Money(100, "BRL")));
    }

    [Fact]
    public void Posting_With_Neither_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new Posting(_accountId, null, null));
    }

    [Fact]
    public void Posting_Amount_Returns_The_Side_That_Is_Filled()
    {
        var debit = Posting.FromDebit(_accountId, new Money(100, "BRL"));
        var credit = Posting.FromCredit(_accountId, new Money(50, "BRL"));

        Assert.Equal(new Money(100, "BRL"), debit.Amount);
        Assert.Equal(new Money(50, "BRL"), credit.Amount);
    }
}
