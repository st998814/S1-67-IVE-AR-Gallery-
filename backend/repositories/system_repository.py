class SystemRepository:
    def health_counts(self, cur):
        cur.execute("SELECT current_database();")
        postgres_database = cur.fetchone()[0]
        cur.execute("SELECT COUNT(*) FROM workspaces;")
        workspaces = cur.fetchone()[0]
        cur.execute("SELECT COUNT(*) FROM targets;")
        targets = cur.fetchone()[0]
        cur.execute("SELECT COUNT(*) FROM contents;")
        contents = cur.fetchone()[0]
        return {
            "postgresDatabase": postgres_database,
            "workspaces": workspaces,
            "targets": targets,
            "contents": contents,
        }

    def workspace_exists(self, cur, workspace_id: str) -> bool:
        cur.execute("SELECT 1 FROM workspaces WHERE workspace_id = %s;", (workspace_id,))
        return cur.fetchone() is not None

    def count_workspace_contents(self, cur, workspace_id: str) -> int:
        cur.execute(
            """
            SELECT COUNT(*) FROM contents c
            INNER JOIN targets t ON c.target_id = t.target_id
            WHERE t.workspace_id = %s;
            """,
            (workspace_id,),
        )
        return int(cur.fetchone()[0])

    def workspace_vuforia_target_ids(self, cur, workspace_id: str):
        cur.execute(
            """
            SELECT DISTINCT vuforia_target_id FROM targets
            WHERE workspace_id = %s
              AND vuforia_target_id IS NOT NULL
              AND TRIM(vuforia_target_id) <> '';
            """,
            (workspace_id,),
        )
        return [row[0].strip() for row in cur.fetchall() if row and row[0]]

    def target_upload_urls(self, cur, workspace_id: str):
        cur.execute("SELECT target_image_url FROM targets WHERE workspace_id = %s;", (workspace_id,))
        return [row[0] for row in cur.fetchall() if row and row[0]]

    def target_reference_upload_urls(self, cur, workspace_id: str):
        cur.execute(
            "SELECT target_reference_image_url FROM targets WHERE workspace_id = %s;",
            (workspace_id,),
        )
        return [row[0] for row in cur.fetchall() if row and row[0]]

    def content_upload_urls(self, cur, workspace_id: str):
        cur.execute(
            """
            SELECT c.media_url FROM contents c
            INNER JOIN targets t ON c.target_id = t.target_id
            WHERE t.workspace_id = %s;
            """,
            (workspace_id,),
        )
        return [row[0] for row in cur.fetchall() if row and row[0]]

    def delete_upload_url(self, cur, url: str):
        cur.execute("DELETE FROM uploads WHERE url = %s;", (url,))

    def delete_workspace_targets(self, cur, workspace_id: str) -> int:
        cur.execute("DELETE FROM targets WHERE workspace_id = %s;", (workspace_id,))
        return cur.rowcount

    def delete_workspace_row(self, cur, workspace_id: str) -> int:
        cur.execute("DELETE FROM workspaces WHERE workspace_id = %s;", (workspace_id,))
        return cur.rowcount
