using Confluent.Kafka;
using Ledger.Outbox;
using Ledger.Outbox.Interfaces;
using Ledger.OutboxDispatcher;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(sp =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("ConnectionStrings:Postgres not configured");
    return NpgsqlDataSource.Create(connectionString);
});

builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var bootstrapServers = sp.GetRequiredService<IConfiguration>()["Kafka:BootstrapServers"]
        ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured");
    return KafkaProducerFactory.Create(bootstrapServers);
});

builder.Services.AddSingleton<IOutboxPublisher>(sp =>
{
    var topic = sp.GetRequiredService<IConfiguration>()["Outbox:Topic"] ?? KafkaOutboxPublisher.DEFAULT_TOPIC;
    return new KafkaOutboxPublisher(sp.GetRequiredService<IProducer<string, string>>(), topic);
});

// ana: por que tudo singleton aqui, e nao Scoped como no ProjectionWorker?
builder.Services.AddSingleton<OutboxDispatcher>();
builder.Services.AddHostedService<OutboxDispatcherService>();

var host = builder.Build();
host.Run();
