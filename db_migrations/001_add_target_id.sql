-- 绑定「内容 ↔ AR Image Target」：与 Unity Authoring 下拉 / ArImageTarget.TargetId 一致。
-- 在 psql 中执行：\i db_migrations/001_add_target_id.sql
ALTER TABLE AR_Content
    ADD COLUMN IF NOT EXISTS TargetId VARCHAR(256) NOT NULL DEFAULT '';
