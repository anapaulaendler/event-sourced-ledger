using Ledger.Domain;
using Ledger.Domain.Exceptions;

namespace Ledger.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void Two_Moneys_With_Same_Amount_And_Currency_Are_Equal()
    {
        var m1 = new Money(100, "BRL");
        var m2 = new Money(100, "BRL");

        Assert.Equal(m1, m2);
        Assert.True(m1 == m2);
    }

    [Fact]
    public void Money_With_Empty_Currency_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Money(100, ""));
    }

    [Fact]
    public void Money_With_NonIso4217_Currency_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Money(100, "REAL"));
    }

    [Fact]
    public void Money_Normalizes_Currency_To_Upper()
    {
        var m = new Money(100, "brl");
        Assert.Equal("BRL", m.Currency);
    }

    [Fact]
    public void Add_Same_Currency_Returns_Sum()
    {
        var sum = new Money(100, "BRL") + new Money(50, "BRL");
        Assert.Equal(new Money(150, "BRL"), sum);
    }

    [Fact]
    public void Add_Different_Currencies_Throws()
    {
        var brl = new Money(100, "BRL");
        var usd = new Money(100, "USD");

        Assert.Throws<CurrencyMismatchException>(() => brl + usd);
    }

    [Fact]
    public void Subtract_Returns_Difference()
    {
        var diff = new Money(100, "BRL") - new Money(30, "BRL");
        Assert.Equal(new Money(70, "BRL"), diff);
    }

    [Fact]
    public void Zero_Returns_Money_With_Zero_Amount()
    {
        var zero = Money.Zero("BRL");
        Assert.Equal(0, zero.AmountCents);
        Assert.Equal("BRL", zero.Currency);
    }
}
