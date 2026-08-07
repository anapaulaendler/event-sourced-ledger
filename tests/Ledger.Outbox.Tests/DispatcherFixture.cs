using Confluent.Kafka;
using Dapper;
using Ledger.Outbox.Interfaces;
using Ledger.OutboxDispatcher;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.Kafka;
using Testcontainers.PostgreSql;

namespace Ledger.Outbox.Tests;

/// <summary>Postgres + Kafka reais: o dispatcher roda contra a mesma infra da producao.</summary>
public sealed class DispatcherFixture : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;
    private KafkaContainer _kafka = null!;
    private IProducer<string, string> _producer = null!;

    public NpgsqlDataSource DataSource { get; private set; } = null!;
    public string BootstrapServers { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("docker.io/library/postgres:16")
            .WithDatabase("ledger_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
        _kafka = new KafkaBuilder("docker.io/confluentinc/cp-kafka:7.6.1").Build();

        await Task.WhenAll(_postgres.StartAsync(), _kafka.StartAsync());

        DefaultTypeMap.MatchNamesWithUnderscores = true;
        DataSource = NpgsqlDataSource.Create(_postgres.GetConnectionString());
        BootstrapServers = _kafka.GetBootstrapAddress();
        _producer = KafkaProducerFactory.Create(BootstrapServers);

        await using var conn = await DataSource.OpenConnectionAsync();
        foreach (var path in new[]
        {
            "../../../../../src/Ledger.EventStore/Migrations/001_CreateEventsTable.sql",
            "../../../../../src/Ledger.Outbox/Migrations/007_CreateOutboxTable.sql"
        })
        {
            await using var cmd = new NpgsqlCommand(await File.ReadAllTextAsync(path), conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public OutboxDispatcher.OutboxDispatcher CreateDispatcher(string topic) =>
        new(DataSource, new KafkaOutboxPublisher(_producer, topic), NullLogger<OutboxDispatcher.OutboxDispatcher>.Instance);

    public OutboxDispatcher.OutboxDispatcher CreateDispatcher(IOutboxPublisher publisher) =>
        new(DataSource, publisher, NullLogger<OutboxDispatcher.OutboxDispatcher>.Instance);

    public async Task<long> AppendRawEventAsync(Guid streamId, int version, string type, string payloadJson)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.ExecuteScalarAsync<long>(
            """
            INSERT INTO events (stream_id, version, type, payload)
            VALUES (@streamId, @version, @type, @payloadJson::jsonb)
            RETURNING sequence
            """,
            new { streamId, version, type, payloadJson });
    }

    public async Task<int> CountByStateAsync(string state)
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        return await conn.ExecuteScalarAsync<int>("SELECT count(*) FROM outbox WHERE state = @state", new { state });
    }

    public async Task ResetAsync()
    {
        await using var conn = await DataSource.OpenConnectionAsync();
        await conn.ExecuteAsync("TRUNCATE outbox, events RESTART IDENTITY CASCADE");
    }

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

    public async Task DisposeAsync()
    {
        _producer.Dispose();
        await DataSource.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _kafka.DisposeAsync().AsTask());
    }
}

[CollectionDefinition(nameof(DispatcherCollection))]
public sealed class DispatcherCollection : ICollectionFixture<DispatcherFixture>;
