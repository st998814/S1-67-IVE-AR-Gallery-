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

    def list_workspaces(self, cur):
        cur.execute(
            """
            SELECT
                w.workspace_id,
                w.workspace_name,
                w.state::text,
                w.schema_version,
                w.created_at_utc,
                w.updated_at_utc,
                COALESCE(tc.target_count, 0) AS target_count,
                COALESCE(cc.content_count, 0) AS content_count,
                COALESCE(preview.target_image_url, '') AS thumbnail_url
            FROM workspaces w
            LEFT JOIN LATERAL (
                SELECT COUNT(*) AS target_count
                FROM targets t
                WHERE t.workspace_id = w.workspace_id
            ) tc ON TRUE
            LEFT JOIN LATERAL (
                SELECT COUNT(*) AS content_count
                FROM contents c
                INNER JOIN targets t ON t.target_id = c.target_id
                WHERE t.workspace_id = w.workspace_id
            ) cc ON TRUE
            LEFT JOIN LATERAL (
                SELECT t.target_image_url
                FROM targets t
                WHERE t.workspace_id = w.workspace_id
                  AND TRIM(COALESCE(t.target_image_url, '')) <> ''
                ORDER BY t.updated_at_utc DESC, t.created_at_utc DESC
                LIMIT 1
            ) preview ON TRUE
            ORDER BY w.updated_at_utc DESC, w.created_at_utc DESC, w.workspace_id ASC;
            """
        )
        return cur.fetchall()

    def get_workspace(self, cur, workspace_id: str):
        cur.execute(
            """
            SELECT
                workspace_id,
                workspace_name,
                state::text,
                schema_version,
                created_at_utc,
                updated_at_utc
            FROM workspaces
            WHERE workspace_id = %s;
            """,
            (workspace_id,),
        )
        return cur.fetchone()

    def list_workspace_targets(self, cur, workspace_id: str):
        cur.execute(
            """
            SELECT
                target_id,
                workspace_id,
                target_name,
                display_label,
                target_image_url,
                target_reference_image_url,
                physical_width_m,
                local_position_x,
                local_position_y,
                local_position_z,
                local_euler_x,
                local_euler_y,
                local_euler_z,
                local_scale_x,
                local_scale_y,
                local_scale_z,
                vuforia_target_id,
                vuforia_status,
                status,
                created_at_utc,
                updated_at_utc
            FROM targets
            WHERE workspace_id = %s
            ORDER BY created_at_utc ASC, target_id ASC;
            """,
            (workspace_id,),
        )
        return cur.fetchall()

    def list_workspace_contents(self, cur, workspace_id: str):
        cur.execute(
            """
            SELECT
                c.content_id,
                c.target_id,
                t.workspace_id,
                c.content_type,
                c.media_url,
                c.local_position_x,
                c.local_position_y,
                c.local_position_z,
                c.local_euler_x,
                c.local_euler_y,
                c.local_euler_z,
                c.local_scale_x,
                c.local_scale_y,
                c.local_scale_z,
                c.render_kind,
                c.asset_format,
                c.meta,
                c.status,
                c.created_at_utc,
                c.updated_at_utc
            FROM contents c
            INNER JOIN targets t ON t.target_id = c.target_id
            WHERE t.workspace_id = %s
            ORDER BY c.created_at_utc ASC, c.content_id ASC;
            """,
            (workspace_id,),
        )
        return cur.fetchall()

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
