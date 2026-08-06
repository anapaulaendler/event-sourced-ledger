namespace Ledger.Outbox;

/// <summary>
/// Linha da tabela outbox. Envelope fica como string crua de proposito: o dispatcher
/// publica os bytes exatos que a trigger gravou, sem round-trip de desserializacao.
/// </summary>
public sealed record OutboxRecord
{
    public long Sequence { get; init; }
    public Guid AggregateId { get; init; }
    public string Envelope { get; init; } = string.Empty;
    public int Attempts { get; init; }
}
