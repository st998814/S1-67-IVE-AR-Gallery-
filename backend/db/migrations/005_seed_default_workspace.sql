INSERT INTO workspaces (workspace_id, workspace_name, state, schema_version, legacy_source)
VALUES ('default', 'Default Workspace', 'ready', 1, FALSE)
ON CONFLICT (workspace_id) DO NOTHING;

