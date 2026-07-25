CREATE TABLE balances (
    account_id UUID NOT NULL,
    currency TEXT NOT NULL,
    balance_cents BIGINT NOT NULL DEFAULT 0,
    last_sequence BIGINT NOT NULL DEFAULT 0,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (account_id, currency)
);
