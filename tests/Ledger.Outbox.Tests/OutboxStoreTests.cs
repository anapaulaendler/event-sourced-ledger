using Dapper;

namespace Ledger.Outbox.Tests;

[Collection(nameof(OutboxCollection))]
public sealed class OutboxStoreTests(OutboxFixture fixture)
{
    [Fact]
    public async Task FetchPending_Returns_Rows_In_Sequence_Order()
    {
        await fixture.ResetAsync();
        await AppendManyAsync(3);

        await using var conn = await fixture.DataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var rows = await OutboxStore.FetchPendingAsync(conn, tx, batchSize: 10, CancellationToken.None);

        Assert.Equal(3, rows.Count);
        Assert.Equal(rows.Select(r => r.Sequence).OrderBy(s => s), rows.Select(r => r.Sequence));
        Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r.Envelope)));
        Assert.All(rows, r => Assert.Equal(0, r.Attempts));
    }

    [Fact]
    public async Task FetchPending_Honours_Batch_Size()
    {
        await fixture.ResetAsync();
        await AppendManyAsync(5);

        await using var conn = await fixture.DataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var rows = await OutboxStore.FetchPendingAsync(conn, tx, batchSize: 2, CancellationToken.None);

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task FetchPending_Skips_Rows_Whose_Next_Attempt_Is_In_The_Future()
    {
        await fixture.ResetAsync();
        await AppendManyAsync(2);

        await using var setup = await fixture.DataSource.OpenConnectionAsync();
        await setup.ExecuteAsync("UPDATE outbox SET next_attempt_at = NOW() + INTERVAL '1 hour' WHERE sequence = 1");

        await using var conn = await fixture.DataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var rows = await OutboxStore.FetchPendingAsync(conn, tx, batchSize: 10, CancellationToken.None);

        Assert.Equal([2L], rows.Select(r => r.Sequence));
    }

    [Fact]
    public async Task FetchPending_Ignores_Rows_Already_Sent()
    {
        await fixture.ResetAsync();
        await AppendManyAsync(2);

        await using var conn = await fixture.DataSource.OpenConnectionAsync();
        await using (var tx = await conn.BeginTransactionAsync())
        {
            await OutboxStore.MarkSentAsync(conn, tx, sequence: 1, CancellationToken.None);
            await tx.CommitAsync();
        }

        await using var readTx = await conn.BeginTransactionAsync();
        var rows = await OutboxStore.FetchPendingAsync(conn, readTx, batchSize: 10, CancellationToken.None);

        Assert.Equal([2L], rows.Select(r => r.Sequence));
    }

    [Fact]
    public async Task MarkSent_Sets_State_And_Published_At()
    {
        await fixture.ResetAsync();
        await AppendManyAsync(1);

        await using var conn = await fixture.DataSource.OpenConnectionAsync();
        await using (var tx = await conn.BeginTransactionAsync())
        {
            await OutboxStore.MarkSentAsync(conn, tx, sequence: 1, CancellationToken.None);
            await tx.CommitAsync();
        }

        var (state, publishedAt) = await conn.QuerySingleAsync<(string, DateTime?)>(
            "SELECT state, published_at FROM outbox WHERE sequence = 1");

        Assert.Equal("sent", state);
        Assert.NotNull(publishedAt);
    }

    /// <summary>
    /// Prova do FOR UPDATE SKIP LOCKED: dois dispatchers concorrentes nunca pegam a mesma linha.
    /// </summary>
    [Fact]
    public async Task Two_Concurrent_Fetches_Never_Claim_The_Same_Row()
    {
        await fixture.ResetAsync();
        await AppendManyAsync(6);

        await using var connA = await fixture.DataSource.OpenConnectionAsync();
        await using var txA = await connA.BeginTransactionAsync();
        var rowsA = await OutboxStore.FetchPendingAsync(connA, txA, batchSize: 3, CancellationToken.None);

        await using var connB = await fixture.DataSource.OpenConnectionAsync();
        await using var txB = await connB.BeginTransactionAsync();
        var rowsB = await OutboxStore.FetchPendingAsync(connB, txB, batchSize: 3, CancellationToken.None);

        var claimedA = rowsA.Select(r => r.Sequence).ToHashSet();
        var claimedB = rowsB.Select(r => r.Sequence).ToHashSet();

        Assert.Equal(3, claimedA.Count);
        Assert.Equal(3, claimedB.Count);
        Assert.Empty(claimedA.Intersect(claimedB));
    }

    private async Task AppendManyAsync(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await fixture.AppendRawEventAsync(Guid.NewGuid(), 1, "TransactionPosted", $$"""{"amountCents":{{(i + 1) * 100}}}""");
        }
    }
}
