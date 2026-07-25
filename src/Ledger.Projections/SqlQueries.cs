namespace Ledger.Projections;

internal static class SqlQueries
{
    public const string UpsertBalance = """
        INSERT INTO balances (account_id, currency, balance_cents, last_sequence, updated_at)
        VALUES (@account_id, @currency, @delta, @sequence, NOW())
        ON CONFLICT (account_id, currency) DO UPDATE
        SET balance_cents = balances.balance_cents + EXCLUDED.balance_cents,
            last_sequence = EXCLUDED.last_sequence,
            updated_at = NOW()
        WHERE balances.last_sequence < EXCLUDED.last_sequence
        """;
}
