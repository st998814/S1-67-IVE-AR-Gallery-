-- Workspace restoration support:
-- 1) query-speed indexes for workspace->targets->contents traversal
-- 2) workspace updated_at touch triggers so list ordering reflects target/content changes

CREATE INDEX IF NOT EXISTS idx_workspaces_updated_at_utc
    ON workspaces (updated_at_utc DESC);

CREATE INDEX IF NOT EXISTS idx_targets_workspace_id_updated_at
    ON targets (workspace_id, updated_at_utc DESC, created_at_utc DESC);

CREATE INDEX IF NOT EXISTS idx_contents_created_at_utc
    ON contents (created_at_utc ASC);

CREATE OR REPLACE FUNCTION touch_workspace_from_target_change()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        UPDATE workspaces SET updated_at_utc = NOW()
        WHERE workspace_id = OLD.workspace_id;
    ELSE
        UPDATE workspaces SET updated_at_utc = NOW()
        WHERE workspace_id = NEW.workspace_id;
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_touch_workspace_from_target_change ON targets;
CREATE TRIGGER trg_touch_workspace_from_target_change
AFTER INSERT OR UPDATE OR DELETE ON targets
FOR EACH ROW
EXECUTE FUNCTION touch_workspace_from_target_change();

CREATE OR REPLACE FUNCTION touch_workspace_from_content_change()
RETURNS TRIGGER AS $$
DECLARE
    old_workspace_id TEXT;
    new_workspace_id TEXT;
BEGIN
    IF TG_OP = 'DELETE' THEN
        SELECT workspace_id INTO old_workspace_id FROM targets WHERE target_id = OLD.target_id;
        IF old_workspace_id IS NOT NULL THEN
            UPDATE workspaces SET updated_at_utc = NOW()
            WHERE workspace_id = old_workspace_id;
        END IF;
        RETURN NULL;
    END IF;

    SELECT workspace_id INTO new_workspace_id FROM targets WHERE target_id = NEW.target_id;
    IF new_workspace_id IS NOT NULL THEN
        UPDATE workspaces SET updated_at_utc = NOW()
        WHERE workspace_id = new_workspace_id;
    END IF;

    IF TG_OP = 'UPDATE' AND OLD.target_id IS DISTINCT FROM NEW.target_id THEN
        SELECT workspace_id INTO old_workspace_id FROM targets WHERE target_id = OLD.target_id;
        IF old_workspace_id IS NOT NULL AND old_workspace_id IS DISTINCT FROM new_workspace_id THEN
            UPDATE workspaces SET updated_at_utc = NOW()
            WHERE workspace_id = old_workspace_id;
        END IF;
    END IF;

    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_touch_workspace_from_content_change ON contents;
CREATE TRIGGER trg_touch_workspace_from_content_change
AFTER INSERT OR UPDATE OR DELETE ON contents
FOR EACH ROW
EXECUTE FUNCTION touch_workspace_from_content_change();
