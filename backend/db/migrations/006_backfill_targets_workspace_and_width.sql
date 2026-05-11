ALTER TABLE targets
    ADD COLUMN IF NOT EXISTS workspace_id TEXT NOT NULL DEFAULT 'default',
    ADD COLUMN IF NOT EXISTS physical_width_m DOUBLE PRECISION NOT NULL DEFAULT 1.0;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'workspaces'
    ) THEN
        IF NOT EXISTS (
            SELECT 1
            FROM information_schema.table_constraints
            WHERE table_schema = 'public'
              AND table_name = 'targets'
              AND constraint_name = 'targets_workspace_id_fkey'
        ) THEN
            ALTER TABLE targets
                ADD CONSTRAINT targets_workspace_id_fkey
                FOREIGN KEY (workspace_id)
                REFERENCES workspaces(workspace_id);
        END IF;
    END IF;
END
$$;

