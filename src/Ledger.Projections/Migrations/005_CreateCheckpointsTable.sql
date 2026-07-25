CREATE TABLE projection_checkpoints (
    projector_name TEXT PRIMARY KEY,
    last_sequence BIGINT NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
