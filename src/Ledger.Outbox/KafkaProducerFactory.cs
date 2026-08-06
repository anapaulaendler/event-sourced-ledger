using Confluent.Kafka;

namespace Ledger.Outbox;

public static class KafkaProducerFactory
{
    /// <summary>
    /// Config canonica do producer. Testes e producao usam esta mesma para nao divergirem
    /// justamente nas garantias de entrega.
    /// </summary>
    public static ProducerConfig CreateConfig(string bootstrapServers) => new()
    {
        BootstrapServers = bootstrapServers,

        // ana: por que Acks.All e nao Acks.Leader? (o que acontece no failover do lider?)
        Acks = Acks.All,

        // ana: idempotence aqui resolve duplicata de ponta a ponta ou so um pedaco dela?
        EnableIdempotence = true,

        MessageSendMaxRetries = 5,
        LingerMs = 5
    };

    public static IProducer<string, string> Create(string bootstrapServers) =>
        new ProducerBuilder<string, string>(CreateConfig(bootstrapServers)).Build();
}
