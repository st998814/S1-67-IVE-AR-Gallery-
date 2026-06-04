from __future__ import annotations

import os

from werkzeug.utils import secure_filename

from repositories.target_repository import TargetRepository


class TargetService:
    def __init__(self, db_connection_factory, repository: TargetRepository | None = None):
        self.db_connection_factory = db_connection_factory
        self.repository = repository or TargetRepository()

    def create_target(self, data):
        with self.db_connection_factory() as conn:
            with conn.cursor() as cur:
                self.repository.ensure_workspace_row(cur, data["workspace_id"], data.get("workspace_name") or None)
                existed = self.repository.target_exists(cur, data["target_id"])
                status = "accepted" if existed else "created"
                row = self.repository.upsert_target(
                    cur,
                    target_id=data["target_id"],
                    workspace_id=data["workspace_id"],
                    target_name=data["target_name"],
                    display_label=data["display_label"],
                    target_image_url=data["target_image_url"],
                    physical_width_m=data["physical_width_m"],
                    local_position=data["local_position"],
                    local_euler=data["local_euler"],
                    local_scale=data["local_scale"],
                    meta=data["meta"],
                    status=status,
                    target_reference_image_url=data.get("target_reference_image_url") or "",
                )
                return row, status, existed

    def upload_target_reference(self, target_id: str, file_storage, *, resolved_ext: str, upload_folder, public_base_url):
        tid = (target_id or "").strip()
        if not tid:
            raise ValueError("targetId is required.")
        if not resolved_ext:
            raise ValueError("resolved_ext is required.")

        image_bytes = file_storage.read()
        if not image_bytes:
            raise ValueError("Target reference image is empty.")

        ref_dir = os.path.join(upload_folder, "target_ref")
        os.makedirs(ref_dir, exist_ok=True)
        dot_ext = resolved_ext if resolved_ext.startswith(".") else f".{resolved_ext}"
        filename = self._disk_filename_for_target_image(tid, f"reference{dot_ext}")
        save_path = os.path.join(ref_dir, filename)
        with open(save_path, "wb") as out:
            out.write(image_bytes)

        file_url = f"{public_base_url.rstrip('/')}/uploads/target_ref/{filename}"

        with self.db_connection_factory() as conn:
            with conn.cursor() as cur:
                if not self.repository.target_exists(cur, tid):
                    return None
                row = self.repository.update_target_reference_image_url(cur, tid, file_url)
                return row

    def create_cloud_target(
        self,
        *,
        file_storage,
        form_data,
        upload_folder,
        public_base_url,
        vuforia_config,
        register_vuforia_target,
    ):
        target_id = form_data["target_id"]
        image_bytes = file_storage.read()
        target_dir = os.path.join(upload_folder, "target")
        os.makedirs(target_dir, exist_ok=True)
        filename = self._disk_filename_for_target_image(target_id, file_storage.filename)
        save_path = os.path.join(target_dir, filename)
        with open(save_path, "wb") as out:
            out.write(image_bytes)

        file_url = f"{public_base_url.rstrip('/')}/uploads/target/{filename}"
        target_width = float(form_data["width"] or vuforia_config.target_width)

        vuforia_result = register_vuforia_target(
            vuforia_config,
            name=target_id,
            image_bytes=image_bytes,
            width=target_width,
            metadata={"targetId": target_id, "targetName": form_data["target_name"]},
        )
        vuforia_target_id = vuforia_result.get("targetId") or ""
        vuforia_status = vuforia_result.get("resultCode") or "created"

        with self.db_connection_factory() as conn:
            with conn.cursor() as cur:
                self.repository.ensure_workspace_row(cur, form_data["workspace_id"], form_data.get("workspace_name") or None)
                existed = self.repository.target_exists(cur, target_id)
                status = "accepted" if existed else "created"
                row = self.repository.upsert_cloud_target(
                    cur,
                    target_id=target_id,
                    workspace_id=form_data["workspace_id"],
                    target_name=form_data["target_name"],
                    display_label=form_data["display_label"],
                    file_url=file_url,
                    target_width=target_width,
                    local_position=form_data["local_position"],
                    local_euler=form_data["local_euler"],
                    local_scale=form_data["local_scale"],
                    vuforia_target_id=vuforia_target_id,
                    vuforia_status=vuforia_status,
                    vuforia_result=vuforia_result,
                    meta=form_data["meta"],
                    status=status,
                )
                return row, status, existed

    def list_targets(self):
        with self.db_connection_factory() as conn:
            with conn.cursor() as cur:
                return self.repository.list_targets(cur)

    def resolve_by_vuforia_id(self, vuforia_target_id: str):
        with self.db_connection_factory() as conn:
            with conn.cursor() as cur:
                return self.repository.find_by_vuforia_id(cur, vuforia_target_id)

    def delete_target(self, target_id: str):
        with self.db_connection_factory() as conn:
            with conn.cursor() as cur:
                return self.repository.delete_target(cur, target_id)

    @staticmethod
    def _disk_filename_for_target_image(target_id: str, source_filename: str) -> str:
        ext = os.path.splitext(source_filename or "")[1].lower()
        if ext not in (".png", ".jpg", ".jpeg", ".gif", ".webp"):
            ext = ".jpg"
        stem = secure_filename((target_id or "").strip()) or "target"
        return f"{stem}{ext}"
