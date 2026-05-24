from __future__ import annotations

from psycopg2.extras import Json


class TargetRepository:
    def ensure_workspace_row(self, cur, workspace_id: str, workspace_name: str | None = None) -> str:
        wid = (workspace_id or "default").strip() or "default"
        wname = (workspace_name or wid).strip() or wid
        cur.execute(
            """
            INSERT INTO workspaces (workspace_id, workspace_name, state, schema_version, legacy_source)
            VALUES (%s, %s, 'ready'::workspace_state, 1, FALSE)
            ON CONFLICT (workspace_id) DO NOTHING;
            """,
            (wid, wname),
        )
        return wid

    def target_exists(self, cur, target_id: str) -> bool:
        cur.execute("SELECT 1 FROM targets WHERE target_id = %s;", (target_id,))
        return cur.fetchone() is not None

    def upsert_target(
        self,
        cur,
        *,
        target_id,
        workspace_id,
        target_name,
        display_label,
        target_image_url,
        physical_width_m,
        local_position,
        local_euler,
        local_scale,
        meta,
        status,
    ):
        cur.execute(
            """
            INSERT INTO targets (
                target_id, workspace_id, target_name, display_label, target_image_url, physical_width_m,
                local_position_x, local_position_y, local_position_z,
                local_euler_x, local_euler_y, local_euler_z,
                local_scale_x, local_scale_y, local_scale_z,
                meta, status
            )
            VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
            ON CONFLICT (target_id) DO UPDATE SET
                workspace_id = EXCLUDED.workspace_id,
                target_name = EXCLUDED.target_name,
                display_label = EXCLUDED.display_label,
                target_image_url = EXCLUDED.target_image_url,
                physical_width_m = EXCLUDED.physical_width_m,
                local_position_x = EXCLUDED.local_position_x,
                local_position_y = EXCLUDED.local_position_y,
                local_position_z = EXCLUDED.local_position_z,
                local_euler_x = EXCLUDED.local_euler_x,
                local_euler_y = EXCLUDED.local_euler_y,
                local_euler_z = EXCLUDED.local_euler_z,
                local_scale_x = EXCLUDED.local_scale_x,
                local_scale_y = EXCLUDED.local_scale_y,
                local_scale_z = EXCLUDED.local_scale_z,
                meta = EXCLUDED.meta,
                status = EXCLUDED.status,
                updated_at_utc = NOW()
            RETURNING target_id, target_name, display_label, target_image_url, status, created_at_utc;
            """,
            (
                target_id,
                workspace_id,
                target_name,
                display_label or target_id,
                target_image_url,
                physical_width_m,
                *local_position,
                *local_euler,
                *local_scale,
                Json(meta),
                status,
            ),
        )
        return cur.fetchone()

    def upsert_cloud_target(
        self,
        cur,
        *,
        target_id,
        workspace_id,
        target_name,
        display_label,
        file_url,
        target_width,
        local_position,
        local_euler,
        local_scale,
        vuforia_target_id,
        vuforia_status,
        vuforia_result,
        meta,
        status,
    ):
        cur.execute(
            """
            INSERT INTO targets (
                target_id, workspace_id, target_name, display_label, target_image_url, physical_width_m,
                local_position_x, local_position_y, local_position_z,
                local_euler_x, local_euler_y, local_euler_z,
                local_scale_x, local_scale_y, local_scale_z,
                vuforia_target_id, vuforia_status, vuforia_result,
                meta, status
            )
            VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
            ON CONFLICT (target_id) DO UPDATE SET
                workspace_id = EXCLUDED.workspace_id,
                target_name = EXCLUDED.target_name,
                display_label = EXCLUDED.display_label,
                target_image_url = EXCLUDED.target_image_url,
                physical_width_m = EXCLUDED.physical_width_m,
                local_position_x = EXCLUDED.local_position_x,
                local_position_y = EXCLUDED.local_position_y,
                local_position_z = EXCLUDED.local_position_z,
                local_euler_x = EXCLUDED.local_euler_x,
                local_euler_y = EXCLUDED.local_euler_y,
                local_euler_z = EXCLUDED.local_euler_z,
                local_scale_x = EXCLUDED.local_scale_x,
                local_scale_y = EXCLUDED.local_scale_y,
                local_scale_z = EXCLUDED.local_scale_z,
                vuforia_target_id = EXCLUDED.vuforia_target_id,
                vuforia_status = EXCLUDED.vuforia_status,
                vuforia_result = EXCLUDED.vuforia_result,
                meta = EXCLUDED.meta,
                status = EXCLUDED.status,
                updated_at_utc = NOW()
            RETURNING target_id, target_name, display_label, target_image_url, status, created_at_utc,
                      vuforia_target_id, vuforia_status;
            """,
            (
                target_id,
                workspace_id,
                target_name,
                display_label or target_id,
                file_url,
                target_width,
                *local_position,
                *local_euler,
                *local_scale,
                vuforia_target_id,
                vuforia_status,
                Json(vuforia_result),
                Json(meta),
                status,
            ),
        )
        return cur.fetchone()

    def list_targets(self, cur):
        cur.execute(
            """
            SELECT target_id, target_name, display_label, target_image_url, status, created_at_utc,
                   vuforia_target_id, vuforia_status
            FROM targets
            ORDER BY created_at_utc ASC, target_id ASC;
            """
        )
        return cur.fetchall()

    def find_by_vuforia_id(self, cur, vuforia_target_id: str):
        cur.execute(
            """
            SELECT target_id, target_name, display_label, target_image_url, status, created_at_utc,
                   vuforia_target_id, vuforia_status
            FROM targets
            WHERE vuforia_target_id = %s;
            """,
            (vuforia_target_id,),
        )
        return cur.fetchone()

    def delete_target(self, cur, target_id: str):
        cur.execute("DELETE FROM targets WHERE target_id = %s RETURNING target_id;", (target_id,))
        return cur.fetchone()
