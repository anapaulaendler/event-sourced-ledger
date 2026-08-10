using System.Text;
using Confluent.Kafka;
using Ledger.Outbox.Interfaces;

namespace Ledger.Outbox;

public sealed class KafkaOutboxPublisher(IProducer<string, string> producer, string topic) : IOutboxPublisher
{
    public const string DEFAULT_TOPIC = "ledger.events";
    public const string EVENT_ID_HEADER = "event_id";

    public async Task PublishAsync(OutboxRecord record, CancellationToken ct)
    {
        var message = new Message<string, string>
        {
            // ana: por que a key e o aggregate_id e nao o sequence?
            Key = record.AggregateId.ToString(),
            Value = record.Envelope,
            Headers =
            [
                new Header(EVENT_ID_HEADER, Encoding.UTF8.GetBytes(record.Sequence.ToString()))
            ]
        };

        await producer.ProduceAsync(topic, message, ct);
    }
}
