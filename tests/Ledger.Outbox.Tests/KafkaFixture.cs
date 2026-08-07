using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Testcontainers.Kafka;

namespace Ledger.Outbox.Tests;

public sealed class KafkaFixture : IAsyncLifetime
{
    private KafkaContainer _kafka = null!;

    public string BootstrapServers { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _kafka = new KafkaBuilder("docker.io/confluentinc/cp-kafka:7.6.1").Build();
        await _kafka.StartAsync();

        BootstrapServers = _kafka.GetBootstrapAddress();
    }

    public IProducer<string, string> CreateProducer() => KafkaProducerFactory.Create(BootstrapServers);

    /// <summary>
    /// Cria o topico com varias particoes de proposito: com o default de 1 particao,
    /// qualquer asserção de "mesma key -> mesma particao" passa por vacuidade.
    /// </summary>
    public async Task CreateTopicAsync(string topic, int partitions = 3)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = BootstrapServers }).Build();
        await admin.CreateTopicsAsync([new TopicSpecification { Name = topic, NumPartitions = partitions, ReplicationFactor = 1 }]);
    }

    /// <summary>
    /// Consumer de teste: le do inicio do topico e para quando o proximo poll estoura o timeout.
    /// </summary>
    public List<ConsumeResult<string, string>> Drain(string topic, int expectedCount, TimeSpan timeout)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = BootstrapServers,
            GroupId = $"test-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(topic);

        var results = new List<ConsumeResult<string, string>>();
        var deadline = DateTime.UtcNow + timeout;

        while (results.Count < expectedCount && DateTime.UtcNow < deadline)
        {
            var result = consumer.Consume(TimeSpan.FromSeconds(1));
            if (result is not null)
            {
                results.Add(result);
            }
        }

        consumer.Close();
        return results;
    }

    public async Task DisposeAsync() => await _kafka.DisposeAsync();
}

[CollectionDefinition(nameof(KafkaCollection))]
public sealed class KafkaCollection : ICollectionFixture<KafkaFixture>;
