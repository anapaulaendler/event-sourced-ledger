CREATE TABLE events (
    sequence BIGSERIAL PRIMARY KEY,
    stream_id UUID NOT NULL,
    version INT NOT NULL,
    type TEXT NOT NULL,
    payload JSONB NOT NULL,
    occurred_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    correlation_id UUID,
    UNIQUE (stream_id, version)
);

CREATE INDEX idx_events_stream_version ON events (stream_id, version);
CREATE INDEX idx_events_type ON events (type);
CREATE INDEX idx_events_payload_gin ON events USING GIN (payload);
