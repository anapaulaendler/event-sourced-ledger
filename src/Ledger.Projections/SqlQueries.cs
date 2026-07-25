namespace Ledger.Projections;

internal static class SqlQueries
{
    public const string InsertStatementLine = """
        INSERT INTO statement (sequence, account_id, occurred_at, debit_cents, credit_cents, currency, running_balance_cents, description)
        SELECT @sequence, @account_id, @occurred_at, @debit_cents, @credit_cents, @currency,
               COALESCE((SELECT running_balance_cents FROM statement
                         WHERE account_id = @account_id AND currency = @currency
                         ORDER BY sequence DESC LIMIT 1), 0) + @delta,
               @description
        ON CONFLICT (sequence, account_id) DO NOTHING
        """;

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
