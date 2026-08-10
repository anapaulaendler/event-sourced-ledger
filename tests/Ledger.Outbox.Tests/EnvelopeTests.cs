using System.Text.Json;
using Dapper;
using Npgsql;

namespace Ledger.Outbox.Tests;

[Collection(nameof(OutboxCollection))]
public sealed class EnvelopeTests(OutboxFixture fixture)
{
    [Fact]
    public async Task Trigger_Builds_Envelope_With_Every_Contract_Field()
    {
        await fixture.ResetAsync();

        var streamId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var sequence = await fixture.AppendRawEventAsync(
            streamId, 1, "TransactionPosted", """{"amountCents":5000,"currency":"BRL"}""", correlationId);

        var envelope = OutboxEnvelope.Parse(await ReadEnvelopeAsync(sequence));

        Assert.Equal(1, envelope.EnvelopeVersion);
        Assert.Equal(sequence, envelope.EventId);
        Assert.Equal(sequence, envelope.Sequence);
        Assert.Equal("TransactionPosted", envelope.EventType);
        Assert.Equal(streamId, envelope.AggregateId);
        Assert.Equal(correlationId, envelope.CorrelationId);
        Assert.Equal(5000, envelope.Payload.GetProperty("amountCents").GetInt64());
        Assert.Equal("BRL", envelope.Payload.GetProperty("currency").GetString());
    }

    [Fact]
    public async Task Envelope_Carries_Null_CorrelationId_When_Event_Has_None()
    {
        await fixture.ResetAsync();

        var sequence = await fixture.AppendRawEventAsync(Guid.NewGuid(), 1, "TransactionReversed", """{"reason":"erro"}""");

        var envelope = OutboxEnvelope.Parse(await ReadEnvelopeAsync(sequence));

        Assert.Null(envelope.CorrelationId);
    }

    [Fact]
    public async Task OccurredAt_Is_Serialized_As_Utc_Iso8601()
    {
        await fixture.ResetAsync();

        var sequence = await fixture.AppendRawEventAsync(Guid.NewGuid(), 1, "TransactionPosted", "{}");

        using var document = JsonDocument.Parse(await ReadEnvelopeAsync(sequence));
        var raw = document.RootElement.GetProperty("occurredAt").GetString();

        Assert.NotNull(raw);
        Assert.EndsWith("Z", raw);
        Assert.Equal(TimeSpan.Zero, DateTimeOffset.Parse(raw).Offset);
    }

    [Fact]
    public async Task Rolled_Back_Append_Leaves_No_Outbox_Row()
    {
        await fixture.ResetAsync();

        await using var conn = await fixture.DataSource.OpenConnectionAsync();
        await using (var tx = await conn.BeginTransactionAsync())
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO events (stream_id, version, type, payload)
                VALUES (@streamId, 1, 'ShouldVanish', '{}'::jsonb)
                """,
                new { streamId = Guid.NewGuid() }, tx);

            Assert.Equal(1, await conn.ExecuteScalarAsync<int>("SELECT count(*) FROM outbox", transaction: tx));

            await tx.RollbackAsync();
        }

        Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT count(*) FROM outbox"));
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT count(*) FROM events"));
    }

    private async Task<string> ReadEnvelopeAsync(long sequence)
    {
        await using var conn = await fixture.DataSource.OpenConnectionAsync();
        return await conn.ExecuteScalarAsync<string>(
            "SELECT envelope::text FROM outbox WHERE sequence = @sequence", new { sequence })
            ?? throw new InvalidOperationException($"Sem linha de outbox para sequence {sequence}");
    }
}
