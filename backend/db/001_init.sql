-- Initial schema for the AR Gallery backend API contract v1.
-- This file is the database baseline. Later changes belong in backend/db/migrations/.

CREATE TABLE IF NOT EXISTS schema_migrations (
    version TEXT PRIMARY KEY,
    applied_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS targets (
    target_id TEXT PRIMARY KEY,
    target_name TEXT NOT NULL,
    display_label TEXT NOT NULL DEFAULT '',
    target_image_url TEXT NOT NULL DEFAULT '',
    local_position_x DOUBLE PRECISION NOT NULL DEFAULT 0,
    local_position_y DOUBLE PRECISION NOT NULL DEFAULT 0,
    local_position_z DOUBLE PRECISION NOT NULL DEFAULT 0,
    local_euler_x DOUBLE PRECISION NOT NULL DEFAULT 0,
    local_euler_y DOUBLE PRECISION NOT NULL DEFAULT 0,
    local_euler_z DOUBLE PRECISION NOT NULL DEFAULT 0,
    local_scale_x DOUBLE PRECISION NOT NULL DEFAULT 1,
    local_scale_y DOUBLE PRECISION NOT NULL DEFAULT 1,
    local_scale_z DOUBLE PRECISION NOT NULL DEFAULT 1,
    vuforia_target_id TEXT NOT NULL DEFAULT '',
    vuforia_status TEXT NOT NULL DEFAULT '',
    vuforia_result JSONB NOT NULL DEFAULT '{}'::jsonb,
    meta JSONB NOT NULL DEFAULT '{}'::jsonb,
    status TEXT NOT NULL DEFAULT 'accepted',
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS contents (
    content_id TEXT PRIMARY KEY,
    target_id TEXT NOT NULL REFERENCES targets(target_id) ON DELETE CASCADE,
    content_type TEXT NOT NULL,
    media_url TEXT NOT NULL DEFAULT '',
    local_position_x DOUBLE PRECISION NOT NULL DEFAULT 0,
    local_position_y DOUBLE PRECISION NOT NULL DEFAULT 0,
    local_position_z DOUBLE PRECISION NOT NULL DEFAULT 0,
    local_euler_x DOUBLE PRECISION NOT NULL DEFAULT 0,
    local_euler_y DOUBLE PRECISION NOT NULL DEFAULT 0,
    local_euler_z DOUBLE PRECISION NOT NULL DEFAULT 0,
    local_scale_x DOUBLE PRECISION NOT NULL DEFAULT 1,
    local_scale_y DOUBLE PRECISION NOT NULL DEFAULT 1,
    local_scale_z DOUBLE PRECISION NOT NULL DEFAULT 1,
    render_kind TEXT NOT NULL DEFAULT '',
    asset_format TEXT NOT NULL DEFAULT '',
    meta JSONB NOT NULL DEFAULT '{}'::jsonb,
    status TEXT NOT NULL DEFAULT 'accepted',
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_contents_target_id ON contents(target_id);

CREATE TABLE IF NOT EXISTS uploads (
    upload_id BIGSERIAL PRIMARY KEY,
    file_name TEXT NOT NULL,
    stored_file_name TEXT NOT NULL,
    mime_type TEXT NOT NULL DEFAULT '',
    size_bytes BIGINT NOT NULL DEFAULT 0,
    url TEXT NOT NULL,
    meta JSONB NOT NULL DEFAULT '{}'::jsonb,
    uploaded_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

