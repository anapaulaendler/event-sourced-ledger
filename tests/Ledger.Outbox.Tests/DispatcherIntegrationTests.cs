using Ledger.Outbox.Interfaces;

namespace Ledger.Outbox.Tests;

[Collection(nameof(DispatcherCollection))]
public sealed class DispatcherIntegrationTests(DispatcherFixture fixture)
{
    private static readonly TimeSpan DRAIN_TIMEOUT = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task Appended_Event_Reaches_Kafka_And_Row_Becomes_Sent()
    {
        await fixture.ResetAsync();
        var topic = NewTopic();

        var streamId = Guid.NewGuid();
        var sequence = await fixture.AppendRawEventAsync(streamId, 1, "TransactionPosted", """{"amountCents":5000}""");

        await fixture.CreateDispatcher(topic).RunOnceAsync(CancellationToken.None);

        var message = Assert.Single(fixture.Drain(topic, expectedCount: 1, DRAIN_TIMEOUT));
        var envelope = OutboxEnvelope.Parse(message.Message.Value);

        Assert.Equal(sequence, envelope.Sequence);
        Assert.Equal(streamId, envelope.AggregateId);
        Assert.Equal("TransactionPosted", envelope.EventType);

        // A key vem do OutboxRecord materializado pelo Dapper, nao do envelope: precisa bater.
        Assert.Equal(streamId.ToString(), message.Message.Key);
        Assert.NotEqual(Guid.Empty.ToString(), message.Message.Key);

        Assert.Equal(1, await fixture.CountByStateAsync("sent"));
        Assert.Equal(0, await fixture.CountByStateAsync("pending"));
    }

    [Fact]
    public async Task Empty_Outbox_Is_A_Noop()
    {
        await fixture.ResetAsync();

        var batchFull = await fixture.CreateDispatcher(NewTopic()).RunOnceAsync(CancellationToken.None);

        Assert.False(batchFull);
    }

    [Fact]
    public async Task Full_Batch_Signals_More_Work_Is_Waiting()
    {
        await fixture.ResetAsync();
        var topic = NewTopic();
        await AppendManyAsync(OutboxDispatcher.OutboxDispatcher.BATCH_SIZE + 5);

        var dispatcher = fixture.CreateDispatcher(topic);

        Assert.True(await dispatcher.RunOnceAsync(CancellationToken.None));
        Assert.False(await dispatcher.RunOnceAsync(CancellationToken.None));

        Assert.Equal(OutboxDispatcher.OutboxDispatcher.BATCH_SIZE + 5, await fixture.CountByStateAsync("sent"));
    }

    [Fact]
    public async Task Every_Appended_Event_Ends_Up_In_Kafka_Exactly_Once()
    {
        await fixture.ResetAsync();
        var topic = NewTopic();
        const int TOTAL = 120;
        await AppendManyAsync(TOTAL);

        var dispatcher = fixture.CreateDispatcher(topic);
        while (await dispatcher.RunOnceAsync(CancellationToken.None)) { }

        Assert.Equal(TOTAL, await fixture.CountByStateAsync("sent"));

        var messages = fixture.Drain(topic, expectedCount: TOTAL, DRAIN_TIMEOUT);
        Assert.Equal(TOTAL, messages.Count);

        var sequences = messages.Select(m => OutboxEnvelope.Parse(m.Message.Value).Sequence).ToList();
        Assert.Equal(TOTAL, sequences.Distinct().Count());
    }

    [Fact]
    public async Task Broker_Failure_Leaves_Row_Pending_And_Publishes_Nothing()
    {
        await fixture.ResetAsync();
        await AppendManyAsync(3);

        var dispatcher = fixture.CreateDispatcher(new AlwaysFailsPublisher());
        await dispatcher.RunOnceAsync(CancellationToken.None);

        Assert.Equal(3, await fixture.CountByStateAsync("pending"));
        Assert.Equal(0, await fixture.CountByStateAsync("sent"));
    }

    [Fact]
    public async Task Row_Reaches_Dead_Letter_After_Max_Attempts()
    {
        await fixture.ResetAsync();
        await AppendManyAsync(1);

        var dispatcher = fixture.CreateDispatcher(new AlwaysFailsPublisher());

        for (var attempt = 0; attempt < RetryPolicy.MAX_ATTEMPTS; attempt++)
        {
            await dispatcher.RunOnceAsync(CancellationToken.None);
            await ClearBackoffAsync();
        }

        Assert.Equal(1, await fixture.CountByStateAsync("dead"));
        Assert.Equal(0, await fixture.CountByStateAsync("pending"));
    }

    [Fact]
    public async Task Recovered_Broker_Publishes_Rows_That_Had_Failed()
    {
        await fixture.ResetAsync();
        var topic = NewTopic();
        await AppendManyAsync(2);

        await fixture.CreateDispatcher(new AlwaysFailsPublisher()).RunOnceAsync(CancellationToken.None);
        Assert.Equal(2, await fixture.CountByStateAsync("pending"));

        await ClearBackoffAsync();
        await fixture.CreateDispatcher(topic).RunOnceAsync(CancellationToken.None);

        Assert.Equal(2, await fixture.CountByStateAsync("sent"));
        Assert.Equal(2, fixture.Drain(topic, expectedCount: 2, DRAIN_TIMEOUT).Count);
    }

    /// <summary>
    /// Dois dispatchers concorrentes sobre o mesmo outbox: nenhuma mensagem duplica no Kafka.
    /// E a garantia que permite escalar o dispatcher horizontalmente.
    /// </summary>
    [Fact]
    public async Task Two_Dispatchers_In_Parallel_Publish_Each_Event_Once()
    {
        await fixture.ResetAsync();
        var topic = NewTopic();
        const int TOTAL = 100;
        await AppendManyAsync(TOTAL);

        var first = fixture.CreateDispatcher(topic);
        var second = fixture.CreateDispatcher(topic);

        await Task.WhenAll(
            DrainLoopAsync(first),
            DrainLoopAsync(second));

        Assert.Equal(TOTAL, await fixture.CountByStateAsync("sent"));

        var messages = fixture.Drain(topic, expectedCount: TOTAL, DRAIN_TIMEOUT);
        var sequences = messages.Select(m => OutboxEnvelope.Parse(m.Message.Value).Sequence).ToList();

        Assert.Equal(TOTAL, sequences.Count);
        Assert.Equal(TOTAL, sequences.Distinct().Count());
    }

    private static async Task DrainLoopAsync(OutboxDispatcher.OutboxDispatcher dispatcher)
    {
        while (await dispatcher.RunOnceAsync(CancellationToken.None)) { }
    }

    private async Task AppendManyAsync(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await fixture.AppendRawEventAsync(Guid.NewGuid(), 1, "TransactionPosted", $$"""{"amountCents":{{(i + 1) * 100}}}""");
        }
    }

    /// <summary>Zera o backoff para o proximo ciclo enxergar a linha sem esperar o relogio.</summary>
    private async Task ClearBackoffAsync()
    {
        await using var conn = await fixture.DataSource.OpenConnectionAsync();
        await using var cmd = new Npgsql.NpgsqlCommand("UPDATE outbox SET next_attempt_at = NOW() WHERE state = 'pending'", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string NewTopic() => $"ledger.dispatch.{Guid.NewGuid():N}";

    private sealed class AlwaysFailsPublisher : IOutboxPublisher
    {
        public Task PublishAsync(OutboxRecord record, CancellationToken ct) =>
            Task.FromException(new InvalidOperationException("broker indisponivel"));
    }
}
