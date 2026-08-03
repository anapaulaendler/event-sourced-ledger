CREATE TABLE outbox (
    sequence BIGINT PRIMARY KEY REFERENCES events(sequence),
    aggregate_id UUID NOT NULL,
    envelope JSONB NOT NULL,
    state TEXT NOT NULL DEFAULT 'pending' CHECK (state IN ('pending', 'sent', 'dead')),
    attempts INT NOT NULL DEFAULT 0,
    next_attempt_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    published_at TIMESTAMPTZ,
    last_error TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ana: por que o indice e parcial (WHERE state = 'pending') e nao um indice comum?
CREATE INDEX idx_outbox_pending ON outbox (next_attempt_at) WHERE state = 'pending';

CREATE OR REPLACE FUNCTION enqueue_outbox()
RETURNS trigger AS $$
BEGIN
    INSERT INTO outbox (sequence, aggregate_id, envelope)
    VALUES (
        NEW.sequence,
        NEW.stream_id,
        jsonb_build_object(
            'envelopeVersion', 1,
            'eventId', NEW.sequence,
            'eventType', NEW.type,
            'aggregateId', NEW.stream_id,
            'sequence', NEW.sequence,
            -- ana: por que to_char explicito em vez de deixar o jsonb serializar o timestamptz?
            'occurredAt', to_char(NEW.occurred_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'),
            'correlationId', NEW.correlation_id,
            'payload', NEW.payload
        )
    );

    PERFORM pg_notify('outbox_pending', NEW.sequence::text);

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER events_enqueue_outbox
AFTER INSERT ON events
FOR EACH ROW EXECUTE FUNCTION enqueue_outbox();
