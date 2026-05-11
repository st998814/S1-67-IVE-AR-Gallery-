DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'workspace_state') THEN
        CREATE TYPE workspace_state AS ENUM ('pending_target_setup', 'ready');
    END IF;
END
$$;

CREATE TABLE IF NOT EXISTS workspaces (
    workspace_id TEXT PRIMARY KEY,
    workspace_name TEXT NOT NULL,
    state workspace_state NOT NULL DEFAULT 'pending_target_setup',
    schema_version INTEGER NOT NULL DEFAULT 1,
    legacy_source BOOLEAN NOT NULL DEFAULT FALSE,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

INSERT INTO workspaces (workspace_id, workspace_name, state, schema_version, legacy_source)
VALUES ('default', 'Default Workspace', 'ready', 1, FALSE)
ON CONFLICT (workspace_id) DO NOTHING;

