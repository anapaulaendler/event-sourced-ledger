using System.Text;
using Confluent.Kafka;

namespace Ledger.Outbox.Tests;

[Collection(nameof(KafkaCollection))]
public sealed class KafkaPublisherTests(KafkaFixture fixture)
{
    private static readonly TimeSpan DRAIN_TIMEOUT = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task Publish_Round_Trips_Envelope_Verbatim()
    {
        var topic = NewTopic();
        var aggregateId = Guid.NewGuid();
        var envelope = $$$"""{"envelopeVersion":1,"eventId":7,"eventType":"TransactionPosted","aggregateId":"{{{aggregateId}}}","sequence":7,"occurredAt":"2026-08-05T10:00:00.000Z","correlationId":null,"payload":{"amountCents":5000}}""";

        using var producer = fixture.CreateProducer();
        var publisher = new KafkaOutboxPublisher(producer, topic);

        await publisher.PublishAsync(
            new OutboxRecord { Sequence = 7, AggregateId = aggregateId, Envelope = envelope },
            CancellationToken.None);

        var messages = fixture.Drain(topic, expectedCount: 1, DRAIN_TIMEOUT);

        var message = Assert.Single(messages);
        Assert.Equal(aggregateId.ToString(), message.Message.Key);
        Assert.Equal(envelope, message.Message.Value);

        var parsed = OutboxEnvelope.Parse(message.Message.Value);
        Assert.Equal("TransactionPosted", parsed.EventType);
        Assert.Equal(aggregateId, parsed.AggregateId);
        Assert.Equal(5000, parsed.Payload.GetProperty("amountCents").GetInt64());
    }

    [Fact]
    public async Task Publish_Sets_Event_Id_Header_For_Downstream_Dedupe()
    {
        var topic = NewTopic();

        using var producer = fixture.CreateProducer();
        var publisher = new KafkaOutboxPublisher(producer, topic);

        await publisher.PublishAsync(
            new OutboxRecord { Sequence = 42, AggregateId = Guid.NewGuid(), Envelope = """{"sequence":42}""" },
            CancellationToken.None);

        var message = Assert.Single(fixture.Drain(topic, expectedCount: 1, DRAIN_TIMEOUT));

        Assert.True(message.Message.Headers.TryGetLastBytes(KafkaOutboxPublisher.EVENT_ID_HEADER, out var raw));
        Assert.Equal("42", Encoding.UTF8.GetString(raw));
    }

    /// <summary>
    /// Ordenacao por agregado: mesma key -> mesma particao -> offsets crescentes.
    /// E o que garante que TransactionReversed nunca chega antes do TransactionPosted dele.
    /// </summary>
    [Fact]
    public async Task Events_Of_Same_Aggregate_Keep_Order_On_One_Partition()
    {
        var topic = NewTopic();
        var aggregateId = Guid.NewGuid();

        using var producer = fixture.CreateProducer();
        var publisher = new KafkaOutboxPublisher(producer, topic);

        for (var sequence = 1; sequence <= 5; sequence++)
        {
            await publisher.PublishAsync(
                new OutboxRecord { Sequence = sequence, AggregateId = aggregateId, Envelope = $$"""{"sequence":{{sequence}}}""" },
                CancellationToken.None);
        }

        var messages = fixture.Drain(topic, expectedCount: 5, DRAIN_TIMEOUT);

        Assert.Equal(5, messages.Count);
        Assert.Single(messages.Select(m => m.Partition.Value).Distinct());
        Assert.Equal(
            messages.Select(m => m.Offset.Value).OrderBy(o => o),
            messages.Select(m => m.Offset.Value));
    }

    [Fact]
    public async Task Different_Aggregates_Are_Keyed_Independently()
    {
        var topic = NewTopic();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        using var producer = fixture.CreateProducer();
        var publisher = new KafkaOutboxPublisher(producer, topic);

        await publisher.PublishAsync(new OutboxRecord { Sequence = 1, AggregateId = first, Envelope = "{}" }, CancellationToken.None);
        await publisher.PublishAsync(new OutboxRecord { Sequence = 2, AggregateId = second, Envelope = "{}" }, CancellationToken.None);

        var messages = fixture.Drain(topic, expectedCount: 2, DRAIN_TIMEOUT);

        Assert.Equal(2, messages.Count);
        Assert.Equal(
            new[] { first.ToString(), second.ToString() }.ToHashSet(),
            messages.Select(m => m.Message.Key).ToHashSet());
    }

    [Fact]
    public async Task Publish_To_Unreachable_Broker_Throws_So_Row_Is_Never_Marked_Sent()
    {
        var config = KafkaProducerFactory.CreateConfig("localhost:1");
        config.MessageTimeoutMs = 2000;
        config.SocketTimeoutMs = 1000;

        using var producer = new ProducerBuilder<string, string>(config).Build();
        var publisher = new KafkaOutboxPublisher(producer, NewTopic());

        await Assert.ThrowsAsync<ProduceException<string, string>>(() =>
            publisher.PublishAsync(
                new OutboxRecord { Sequence = 1, AggregateId = Guid.NewGuid(), Envelope = "{}" },
                CancellationToken.None));
    }

    private static string NewTopic() => $"ledger.events.{Guid.NewGuid():N}";
}
