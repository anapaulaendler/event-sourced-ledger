CREATE TABLE statement (
    sequence BIGINT NOT NULL,
    account_id UUID NOT NULL,
    occurred_at TIMESTAMPTZ NOT NULL,
    debit_cents BIGINT,
    credit_cents BIGINT,
    currency TEXT NOT NULL,
    running_balance_cents BIGINT NOT NULL,
    description TEXT,
    PRIMARY KEY (sequence, account_id)
);

CREATE INDEX idx_statement_account_time ON statement (account_id, occurred_at);
