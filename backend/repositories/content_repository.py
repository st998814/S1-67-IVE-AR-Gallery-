CONTENT_DETAIL_SELECT = """
    SELECT
        content_id, target_id, content_type, media_url,
        local_position_x, local_position_y, local_position_z,
        local_euler_x, local_euler_y, local_euler_z,
        local_scale_x, local_scale_y, local_scale_z,
        render_kind, asset_format, meta, status, created_at_utc, updated_at_utc
    FROM contents
"""


class ContentRepository:
    def target_exists(self, cur, target_id: str) -> bool:
        cur.execute("SELECT 1 FROM targets WHERE target_id = %s;", (target_id,))
        return cur.fetchone() is not None

    def content_exists(self, cur, content_id: str) -> bool:
        cur.execute("SELECT 1 FROM contents WHERE content_id = %s;", (content_id,))
        return cur.fetchone() is not None

    def upsert_content(
        self,
        cur,
        *,
        content_id,
        target_id,
        content_type,
        media_url,
        local_position,
        local_euler,
        local_scale,
        render_kind,
        asset_format,
        meta_json,
        status,
    ):
        cur.execute(
            """
            INSERT INTO contents (
                content_id, target_id, content_type, media_url,
                local_position_x, local_position_y, local_position_z,
                local_euler_x, local_euler_y, local_euler_z,
                local_scale_x, local_scale_y, local_scale_z,
                render_kind, asset_format, meta, status
            )
            VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
            ON CONFLICT (content_id) DO UPDATE SET
                target_id = EXCLUDED.target_id,
                content_type = EXCLUDED.content_type,
                media_url = EXCLUDED.media_url,
                local_position_x = EXCLUDED.local_position_x,
                local_position_y = EXCLUDED.local_position_y,
                local_position_z = EXCLUDED.local_position_z,
                local_euler_x = EXCLUDED.local_euler_x,
                local_euler_y = EXCLUDED.local_euler_y,
                local_euler_z = EXCLUDED.local_euler_z,
                local_scale_x = EXCLUDED.local_scale_x,
                local_scale_y = EXCLUDED.local_scale_y,
                local_scale_z = EXCLUDED.local_scale_z,
                render_kind = EXCLUDED.render_kind,
                asset_format = EXCLUDED.asset_format,
                meta = EXCLUDED.meta,
                status = EXCLUDED.status,
                updated_at_utc = NOW()
            RETURNING content_id, target_id, status, created_at_utc;
            """,
            (
                content_id,
                target_id,
                content_type,
                media_url,
                *local_position,
                *local_euler,
                *local_scale,
                render_kind,
                asset_format,
                meta_json,
                status,
            ),
        )
        return cur.fetchone()

    def list_content(self, cur, target_id: str = ""):
        if target_id:
            cur.execute(
                CONTENT_DETAIL_SELECT
                + """
                WHERE target_id = %s
                ORDER BY created_at_utc ASC, content_id ASC;
                """,
                (target_id,),
            )
        else:
            cur.execute(
                CONTENT_DETAIL_SELECT
                + """
                ORDER BY created_at_utc ASC, content_id ASC;
                """
            )
        return cur.fetchall()

    def get_content(self, cur, content_id: str):
        cur.execute(CONTENT_DETAIL_SELECT + " WHERE content_id = %s;", (content_id,))
        return cur.fetchone()

    def get_existing_patch_values(self, cur, content_id: str):
        cur.execute("SELECT content_type, media_url FROM contents WHERE content_id = %s;", (content_id,))
        return cur.fetchone()

    def patch_content(self, cur, *, updates, params):
        cur.execute(
            f"""
            UPDATE contents
            SET {', '.join(updates)}, status = 'accepted', updated_at_utc = NOW()
            WHERE content_id = %s
            RETURNING content_id, target_id, status, created_at_utc;
            """,
            params,
        )
        return cur.fetchone()

    def delete_content(self, cur, content_id: str):
        cur.execute("DELETE FROM contents WHERE content_id = %s RETURNING content_id;", (content_id,))
        return cur.fetchone()

    def find_mobile_target(self, cur, target_key: str):
        cur.execute(
            """
            SELECT target_id, target_name, display_label, physical_width_m,
                   local_euler_x, local_euler_y, local_euler_z
            FROM targets
            WHERE target_id = %s OR vuforia_target_id = %s
            LIMIT 1;
            """,
            (target_key, target_key),
        )
        return cur.fetchone()

    def first_content_for_target(self, cur, target_id: str):
        cur.execute(
            """
            SELECT
                content_type,
                media_url,
                local_position_x, local_position_y, local_position_z,
                local_euler_x, local_euler_y, local_euler_z,
                local_scale_x, local_scale_y, local_scale_z,
                meta
            FROM contents
            WHERE target_id = %s
            ORDER BY created_at_utc ASC, content_id ASC
            LIMIT 1;
            """,
            (target_id,),
        )
        return cur.fetchone()
