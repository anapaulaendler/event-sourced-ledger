using Dapper;
using Npgsql;

namespace Ledger.Outbox.Tests;

[Collection(nameof(OutboxCollection))]
public sealed class OutboxRetryTests(OutboxFixture fixture)
{
    [Fact]
    public async Task First_Failure_Keeps_Row_Pending_And_Backs_Off_One_Second()
    {
        await fixture.ResetAsync();
        var record = await SingleRecordAsync();

        var movedToDead = await MarkFailedAsync(record, "broker indisponivel");

        Assert.False(movedToDead);

        var row = await ReadRowAsync(record.Sequence);
        Assert.Equal("pending", row.State);
        Assert.Equal(1, row.Attempts);
        Assert.Equal("broker indisponivel", row.LastError);
        Assert.InRange(row.SecondsUntilNextAttempt, 0.5, 1.5);
    }

    [Fact]
    public async Task Failed_Row_Is_Invisible_Until_Its_Backoff_Elapses()
    {
        await fixture.ResetAsync();
        var record = await SingleRecordAsync();
        await MarkFailedAsync(record, "erro");

        await using var conn = await fixture.DataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var pending = await OutboxStore.FetchPendingAsync(conn, tx, batchSize: 10, CancellationToken.None);

        Assert.Empty(pending);
    }

    [Fact]
    public async Task Seven_Failures_Stay_Pending_And_The_Eighth_Goes_To_Dead_Letter()
    {
        await fixture.ResetAsync();
        await fixture.AppendRawEventAsync(Guid.NewGuid(), 1, "TransactionPosted", "{}");

        var deadAt = 0;
        for (var attempt = 1; attempt <= RetryPolicy.MAX_ATTEMPTS; attempt++)
        {
            var current = await ReadRowAsync(1);
            var record = new OutboxRecord { Sequence = 1, AggregateId = Guid.NewGuid(), Attempts = current.Attempts };

            if (await MarkFailedAsync(record, $"falha {attempt}"))
            {
                deadAt = attempt;
                break;
            }

            Assert.Equal("pending", (await ReadRowAsync(1)).State);
        }

        Assert.Equal(RetryPolicy.MAX_ATTEMPTS, deadAt);

        var final = await ReadRowAsync(1);
        Assert.Equal("dead", final.State);
        Assert.Equal(RetryPolicy.MAX_ATTEMPTS, final.Attempts);
        Assert.Equal($"falha {RetryPolicy.MAX_ATTEMPTS}", final.LastError);
    }

    [Fact]
    public async Task Dead_Rows_Are_Never_Fetched_Again()
    {
        await fixture.ResetAsync();
        await fixture.AppendRawEventAsync(Guid.NewGuid(), 1, "TransactionPosted", "{}");

        await using var setup = await fixture.DataSource.OpenConnectionAsync();
        await setup.ExecuteAsync("UPDATE outbox SET state = 'dead', next_attempt_at = NOW() - INTERVAL '1 day'");

        await using var conn = await fixture.DataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var pending = await OutboxStore.FetchPendingAsync(conn, tx, batchSize: 10, CancellationToken.None);

        Assert.Empty(pending);
    }

    [Fact]
    public async Task Dead_Row_Can_Be_Requeued_By_Hand()
    {
        await fixture.ResetAsync();
        await fixture.AppendRawEventAsync(Guid.NewGuid(), 1, "TransactionPosted", "{}");

        await using var setup = await fixture.DataSource.OpenConnectionAsync();
        await setup.ExecuteAsync("UPDATE outbox SET state = 'dead', attempts = 8");
        await setup.ExecuteAsync(
            "UPDATE outbox SET state = 'pending', attempts = 0, next_attempt_at = NOW() WHERE state = 'dead'");

        await using var conn = await fixture.DataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var pending = await OutboxStore.FetchPendingAsync(conn, tx, batchSize: 10, CancellationToken.None);

        Assert.Single(pending);
    }

    [Fact]
    public async Task Oversized_Error_Is_Truncated_Instead_Of_Bloating_The_Row()
    {
        await fixture.ResetAsync();
        var record = await SingleRecordAsync();

        await MarkFailedAsync(record, new string('x', 10_000));

        var row = await ReadRowAsync(record.Sequence);
        Assert.Equal(OutboxStore.MAX_ERROR_LENGTH, row.LastError!.Length);
    }

    [Fact]
    public async Task Successful_Publish_After_Failures_Clears_The_Error()
    {
        await fixture.ResetAsync();
        var record = await SingleRecordAsync();
        await MarkFailedAsync(record, "falha transitoria");

        await using var conn = await fixture.DataSource.OpenConnectionAsync();
        await using (var tx = await conn.BeginTransactionAsync())
        {
            await OutboxStore.MarkSentAsync(conn, tx, record.Sequence, CancellationToken.None);
            await tx.CommitAsync();
        }

        var row = await ReadRowAsync(record.Sequence);
        Assert.Equal("sent", row.State);
        Assert.Null(row.LastError);
        Assert.Equal(1, row.Attempts);
    }

    private async Task<OutboxRecord> SingleRecordAsync()
    {
        await fixture.AppendRawEventAsync(Guid.NewGuid(), 1, "TransactionPosted", "{}");

        await using var conn = await fixture.DataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var rows = await OutboxStore.FetchPendingAsync(conn, tx, batchSize: 1, CancellationToken.None);
        await tx.CommitAsync();

        return rows.Single();
    }

    private async Task<bool> MarkFailedAsync(OutboxRecord record, string error)
    {
        await using var conn = await fixture.DataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var movedToDead = await OutboxStore.MarkFailedAsync(conn, tx, record, error, CancellationToken.None);
        await tx.CommitAsync();

        return movedToDead;
    }

    private async Task<OutboxRow> ReadRowAsync(long sequence)
    {
        await using var conn = await fixture.DataSource.OpenConnectionAsync();
        return await conn.QuerySingleAsync<OutboxRow>(
            """
            SELECT state, attempts, last_error AS lasterror,
                   EXTRACT(EPOCH FROM (next_attempt_at - NOW())) AS secondsuntilnextattempt
            FROM outbox WHERE sequence = @sequence
            """,
            new { sequence });
    }

    private sealed record OutboxRow
    {
        public string State { get; init; } = string.Empty;
        public int Attempts { get; init; }
        public string? LastError { get; init; }
        public double SecondsUntilNextAttempt { get; init; }
    }
}
