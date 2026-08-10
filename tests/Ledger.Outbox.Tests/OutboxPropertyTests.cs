using Dapper;

namespace Ledger.Outbox.Tests;

[Collection(nameof(DispatcherCollection))]
public sealed class OutboxPropertyTests(DispatcherFixture fixture)
{
    private static readonly TimeSpan DRAIN_TIMEOUT = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Bijecao: toda linha marcada 'sent' tem exatamente uma mensagem no Kafka, e vice-versa.
    /// Nada se perde, nada duplica.
    /// </summary>
    [Theory]
    [InlineData(11, 4, 37)]
    [InlineData(1337, 9, 80)]
    [InlineData(2026, 1, 55)]
    public async Task Sent_Rows_And_Kafka_Messages_Are_In_Bijection(int seed, int aggregateCount, int eventCount)
    {
        await fixture.ResetAsync();
        var topic = NewTopic();

        var expected = await AppendRandomEventsAsync(seed, aggregateCount, eventCount);

        var dispatcher = fixture.CreateDispatcher(topic);
        while (await dispatcher.RunOnceAsync(CancellationToken.None)) { }

        Assert.Equal(eventCount, await fixture.CountByStateAsync("sent"));
        Assert.Equal(0, await fixture.CountByStateAsync("pending"));
        Assert.Equal(0, await fixture.CountByStateAsync("dead"));

        var messages = fixture.Drain(topic, eventCount, DRAIN_TIMEOUT);
        var published = messages
            .Select(m => OutboxEnvelope.Parse(m.Message.Value).Sequence)
            .ToList();

        Assert.Equal(eventCount, published.Count);
        Assert.Equal(eventCount, published.Distinct().Count());
        Assert.Equal(expected.Keys.Order(), published.Order());
    }

    /// <summary>
    /// A key do Kafka precisa bater com o aggregate_id do envelope em TODA mensagem —
    /// e o que sustenta o particionamento por agregado.
    /// </summary>
    [Theory]
    [InlineData(7, 5, 40)]
    [InlineData(99, 3, 25)]
    public async Task Every_Message_Key_Matches_Its_Envelope_Aggregate(int seed, int aggregateCount, int eventCount)
    {
        await fixture.ResetAsync();
        var topic = NewTopic();

        await AppendRandomEventsAsync(seed, aggregateCount, eventCount);

        var dispatcher = fixture.CreateDispatcher(topic);
        while (await dispatcher.RunOnceAsync(CancellationToken.None)) { }

        var messages = fixture.Drain(topic, eventCount, DRAIN_TIMEOUT);
        Assert.Equal(eventCount, messages.Count);

        Assert.All(messages, message =>
        {
            var envelope = OutboxEnvelope.Parse(message.Message.Value);
            Assert.Equal(envelope.AggregateId.ToString(), message.Message.Key);
            Assert.NotEqual(Guid.Empty, envelope.AggregateId);
        });
    }

    /// <summary>
    /// O fan-out dispara N ProduceAsync em paralelo. Como as chamadas sao enfileiradas em ordem
    /// de sequence e o producer e idempotente, a ordem por agregado tem que sobreviver.
    /// </summary>
    [Fact]
    public async Task Order_Per_Aggregate_Survives_The_Concurrent_Fanout()
    {
        await fixture.ResetAsync();
        var topic = NewTopic();
        await fixture.CreateTopicAsync(topic, partitions: 3);

        var aggregates = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        foreach (var version in Enumerable.Range(1, 12))
        {
            foreach (var aggregate in aggregates)
            {
                await fixture.AppendRawEventAsync(aggregate, version, "TransactionPosted", $$"""{"v":{{version}}}""");
            }
        }

        var total = aggregates.Count * 12;
        var dispatcher = fixture.CreateDispatcher(topic);
        while (await dispatcher.RunOnceAsync(CancellationToken.None)) { }

        var messages = fixture.Drain(topic, total, DRAIN_TIMEOUT);
        Assert.Equal(total, messages.Count);

        foreach (var group in messages.GroupBy(m => m.Message.Key))
        {
            var byOffset = group.OrderBy(m => m.Partition.Value).ThenBy(m => m.Offset.Value)
                .Select(m => OutboxEnvelope.Parse(m.Message.Value).Sequence)
                .ToList();

            Assert.Equal(byOffset.OrderBy(s => s), byOffset);
            Assert.Single(group.Select(m => m.Partition.Value).Distinct());
        }

        // O topico tem 3 particoes: se as mensagens caissem todas numa so, as asserções
        // de ordem acima nao provariam nada sobre o roteamento por key.
        Assert.True(messages.Select(m => m.Partition.Value).Distinct().Count() > 1,
            "todas as mensagens caíram na mesma partição — o teste de ordenação seria vacuo");
    }

    /// <summary>
    /// Eventos chegando enquanto dois dispatchers ja estao rodando: nada fica para tras
    /// nem sai duplicado. E o cenario real de producao.
    /// </summary>
    [Fact]
    public async Task Concurrent_Appends_And_Dispatchers_Lose_Nothing()
    {
        await fixture.ResetAsync();
        var topic = NewTopic();
        const int TOTAL = 150;

        var appending = Task.Run(async () =>
        {
            for (var i = 0; i < TOTAL; i++)
            {
                await fixture.AppendRawEventAsync(Guid.NewGuid(), 1, "TransactionPosted", $$"""{"i":{{i}}}""");
            }
        });

        var first = fixture.CreateDispatcher(topic);
        var second = fixture.CreateDispatcher(topic);

        var dispatching = Task.WhenAll(
            PumpUntilAsync(first, appending),
            PumpUntilAsync(second, appending));

        await appending;
        await dispatching;

        // Varredura final: o que entrou depois do ultimo ciclo.
        while (await first.RunOnceAsync(CancellationToken.None)) { }
        await first.RunOnceAsync(CancellationToken.None);

        Assert.Equal(TOTAL, await CountAsync("SELECT count(*) FROM outbox"));
        Assert.Equal(TOTAL, await fixture.CountByStateAsync("sent"));

        var messages = fixture.Drain(topic, TOTAL, DRAIN_TIMEOUT);
        var sequences = messages.Select(m => OutboxEnvelope.Parse(m.Message.Value).Sequence).ToList();

        Assert.Equal(TOTAL, sequences.Count);
        Assert.Equal(TOTAL, sequences.Distinct().Count());
    }

    private static async Task PumpUntilAsync(OutboxDispatcher.OutboxDispatcher dispatcher, Task appending)
    {
        while (!appending.IsCompleted)
        {
            await dispatcher.RunOnceAsync(CancellationToken.None);
        }
    }

    private async Task<Dictionary<long, Guid>> AppendRandomEventsAsync(int seed, int aggregateCount, int eventCount)
    {
        var random = new Random(seed);
        var aggregates = Enumerable.Range(0, aggregateCount).Select(_ => Guid.NewGuid()).ToList();
        var versions = aggregates.ToDictionary(id => id, _ => 0);
        var appended = new Dictionary<long, Guid>();

        for (var i = 0; i < eventCount; i++)
        {
            var aggregate = aggregates[random.Next(aggregates.Count)];
            var version = ++versions[aggregate];
            var type = random.Next(2) == 0 ? "TransactionPosted" : "TransactionReversed";

            var sequence = await fixture.AppendRawEventAsync(aggregate, version, type, $$"""{"amountCents":{{random.Next(1, 100_000)}}}""");
            appended[sequence] = aggregate;
        }

        return appended;
    }

    private async Task<int> CountAsync(string sql)
    {
        await using var conn = await fixture.DataSource.OpenConnectionAsync();
        return await conn.ExecuteScalarAsync<int>(sql);
    }

    private static string NewTopic() => $"ledger.property.{Guid.NewGuid():N}";
}
