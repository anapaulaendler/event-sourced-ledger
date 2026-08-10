using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ledger.Outbox;

/// <summary>
/// Contrato publico publicado no Kafka. Montado pela trigger enqueue_outbox (migration 007),
/// nunca por codigo C# — esta classe existe para leitura/asserção, nao para producao.
/// </summary>
public sealed record OutboxEnvelope
{
    public int EnvelopeVersion { get; init; }
    public long EventId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public Guid AggregateId { get; init; }
    public long Sequence { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public Guid? CorrelationId { get; init; }
    public JsonElement Payload { get; init; }

    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static OutboxEnvelope Parse(string json) =>
        JsonSerializer.Deserialize<OutboxEnvelope>(json, SerializerOptions)
        ?? throw new InvalidOperationException("Envelope JSON desserializou para null");
}
