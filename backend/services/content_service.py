from psycopg2.extras import Json

from api.serializers import vector_response
from repositories.content_repository import ContentRepository


CONTENT_TYPES_ALLOWED = {"image", "video", "model"}
CONTENT_TYPES_REQUIRING_MEDIA = {"image", "video", "model"}


class ContentService:
    def __init__(self, db_connection_factory, repository=None):
        self.db_connection_factory = db_connection_factory
        self.repository = repository or ContentRepository()

    def create_content(self, data):
        with self.db_connection_factory() as conn:
            with conn.cursor() as cur:
                if not self.repository.target_exists(cur, data["target_id"]):
                    return None, None, False, "target_not_found"

                existed = self.repository.content_exists(cur, data["content_id"])
                status = "accepted" if existed else "created"
                row = self.repository.upsert_content(
                    cur,
                    content_id=data["content_id"],
                    target_id=data["target_id"],
                    content_type=data["content_type"],
                    media_url=data["media_url"],
                    local_position=data["local_position"],
                    local_euler=data["local_euler"],
                    local_scale=data["local_scale"],
                    render_kind=data["render_kind"],
                    asset_format=data["asset_format"],
                    meta_json=Json(data["meta"]),
                    status=status,
                )
                return row, status, existed, None

    def list_content(self, target_id: str = ""):
        with self.db_connection_factory() as conn:
            with conn.cursor() as cur:
                return self.repository.list_content(cur, target_id)

    def get_content(self, content_id: str):
        with self.db_connection_factory() as conn:
            with conn.cursor() as cur:
                return self.repository.get_content(cur, content_id)

    def patch_content(self, content_id: str, *, updates, params, target_id=None, content_type=None, media_url=None):
        with self.db_connection_factory() as conn:
            with conn.cursor() as cur:
                existing = self.repository.get_existing_patch_values(cur, content_id)
                if existing is None:
                    return None, "content_not_found"

                final_content_type = content_type or existing[0]
                final_media_url = media_url if media_url is not None else existing[1]
                if final_content_type.lower() in CONTENT_TYPES_REQUIRING_MEDIA and not final_media_url:
                    return None, "media_required"

                if target_id is not None and not self.repository.target_exists(cur, target_id):
                    return None, "target_not_found"

                params.append(content_id)
                return self.repository.patch_content(cur, updates=updates, params=params), None

    def delete_content(self, content_id: str):
        with self.db_connection_factory() as conn:
            with conn.cursor() as cur:
                return self.repository.delete_content(cur, content_id)

    def mobileviewer_content_by_target(self, target_key: str, *, public_base_url: str, host_url: str):
        with self.db_connection_factory() as conn:
            with conn.cursor() as cur:
                target_row = self.repository.find_mobile_target(cur, target_key)
                if target_row is None:
                    return None, "target_not_found"

                target_id, target_name, display_label, physical_width_m, target_euler_x, target_euler_y, target_euler_z = (
                    target_row[0],
                    target_row[1],
                    target_row[2],
                    target_row[3],
                    target_row[4],
                    target_row[5],
                    target_row[6],
                )

                content_row = self.repository.first_content_for_target(cur, target_id)
                if content_row is None:
                    return {"targetId": target_id}, "content_not_found"

                return self._mobile_response(
                    target_id=target_id,
                    target_name=target_name,
                    display_label=display_label,
                    physical_width_m=physical_width_m,
                    target_euler_x=target_euler_x,
                    target_euler_y=target_euler_y,
                    target_euler_z=target_euler_z,
                    content_row=content_row,
                    public_base_url=public_base_url,
                    host_url=host_url,
                ), None

    def _mobile_response(
        self,
        *,
        target_id,
        target_name,
        display_label,
        physical_width_m,
        target_euler_x,
        target_euler_y,
        target_euler_z,
        content_row,
        public_base_url,
        host_url,
    ):
        (
            content_type,
            media_url,
            local_pos_x,
            local_pos_y,
            local_pos_z,
            local_euler_x,
            local_euler_y,
            local_euler_z,
            local_scale_x,
            local_scale_y,
            local_scale_z,
            meta,
        ) = content_row
        if not isinstance(meta, dict):
            meta = {}

        source_type = (content_type or "").strip().lower()
        runtime_type = source_type if source_type in CONTENT_TYPES_ALLOWED else "image"
        posture = self._target_posture(target_euler_x)

        color = (meta.get("color") or meta.get("mockColor") or meta.get("tint") or "").strip()
        if not color:
            if runtime_type == "capsule":
                color = "#F39C12"
            elif runtime_type == "sphere":
                color = "#2ECC71"
            else:
                color = "#4EA3F5"

        media_url = (media_url or "").strip()
        if media_url.startswith(public_base_url):
            media_url = host_url.rstrip("/") + media_url[len(public_base_url.rstrip("/")) :]

        return {
            "targetName": target_name or target_id,
            "title": (meta.get("title") or "").strip(),
            "description": (meta.get("description") or "").strip(),
            "contentType": runtime_type,
            "mediaUrl": media_url,
            "localPosition": vector_response(local_pos_x, local_pos_y, local_pos_z),
            "localEuler": vector_response(local_euler_x, local_euler_y, local_euler_z),
            "localScale": vector_response(local_scale_x, local_scale_y, local_scale_z),
            "targetLocalEuler": vector_response(target_euler_x or 0.0, target_euler_y or 0.0, target_euler_z or 0.0),
            "targetPosture": posture,
            "color": color,
            "displayLabel": (display_label or "").strip() or (target_name or target_id),
            "targetPhysicalWidthM": float(physical_width_m) if physical_width_m is not None else 1.0,
        }

    @staticmethod
    def _target_posture(target_euler_x):
        """Match AuthoringTool WorkspacePresetLibrary / InferPostureFromTargetLocalEuler (+90° X = floor)."""
        posture = "wall"
        if target_euler_x is not None:
            x = float(target_euler_x)
            if x >= 45.0:
                posture = "floor"
            elif x <= -45.0:
                posture = "ceiling"
        return posture
