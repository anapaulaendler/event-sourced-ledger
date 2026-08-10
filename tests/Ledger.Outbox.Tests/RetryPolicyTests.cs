namespace Ledger.Outbox.Tests;

public sealed class RetryPolicyTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    [InlineData(6, 32)]
    [InlineData(7, 60)]
    public void ComputeDelay_Doubles_Until_It_Hits_The_Cap(int attempts, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), RetryPolicy.ComputeDelay(attempts));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(50)]
    [InlineData(int.MaxValue)]
    public void ComputeDelay_Never_Exceeds_The_Cap(int attempts)
    {
        Assert.Equal(TimeSpan.FromSeconds(60), RetryPolicy.ComputeDelay(attempts));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ComputeDelay_Rejects_Attempt_Counts_Below_One(int attempts)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RetryPolicy.ComputeDelay(attempts));
    }

    [Fact]
    public void ComputeDelay_Is_Monotonic()
    {
        var delays = Enumerable.Range(1, 20).Select(RetryPolicy.ComputeDelay).ToList();

        Assert.Equal(delays.OrderBy(d => d), delays);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(9, true)]
    public void ShouldMoveToDead_Trips_At_Max_Attempts(int attempts, bool expected)
    {
        Assert.Equal(expected, RetryPolicy.ShouldMoveToDead(attempts));
    }

    /// <summary>
    /// Orcamento total de retry: o tempo que uma linha sobrevive antes de virar dead.
    /// Se o Kafka voltar dentro dessa janela, nada vai pra dead-letter.
    /// </summary>
    [Fact]
    public void Total_Retry_Window_Is_About_Two_Minutes()
    {
        var total = Enumerable.Range(1, RetryPolicy.MAX_ATTEMPTS - 1)
            .Select(RetryPolicy.ComputeDelay)
            .Aggregate(TimeSpan.Zero, (sum, delay) => sum + delay);

        Assert.Equal(TimeSpan.FromSeconds(123), total);
    }
}
