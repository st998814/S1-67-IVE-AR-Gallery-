from __future__ import annotations

import os
from typing import Callable, Optional

from repositories.system_repository import SystemRepository
from vuforia_service import VuforiaConfig, VuforiaError


class SystemService:
    def __init__(
        self,
        db_connection_factory,
        *,
        public_base_url,
        upload_folder,
        repository=None,
        vuforia_config: Optional[VuforiaConfig] = None,
        delete_vuforia_target_fn: Optional[Callable] = None,
    ):
        self.db_connection_factory = db_connection_factory
        self.public_base_url = public_base_url
        self.upload_folder = upload_folder
        self.repository = repository or SystemRepository()
        self.vuforia_config = vuforia_config
        self.delete_vuforia_target_fn = delete_vuforia_target_fn

    def health(self, configured_db_name: str):
        body = {"ok": True, "publicBaseUrl": self.public_base_url, "configuredDbName": configured_db_name}
        with self.db_connection_factory() as conn:
            with conn.cursor() as cur:
                body.update(self.repository.health_counts(cur))
        return body

    def list_workspaces(self):
        with self.db_connection_factory() as conn:
            with conn.cursor() as cur:
                rows = self.repository.list_workspaces(cur)
                enriched = []
                for row in rows:
                    if row is None:
                        continue
                    workspace_id = row[0]
                    thumbnail_url = row[8] or ""
                    if not thumbnail_url:
                        thumbnail_url = self._resolve_workspace_thumbnail_fallback(cur, workspace_id)
                    if thumbnail_url:
                        row = tuple(list(row[:8]) + [thumbnail_url])
                    enriched.append(row)
                return enriched

    def get_workspace_restore_payload(self, workspace_id: str):
        wid = (workspace_id or "").strip()
        with self.db_connection_factory() as conn:
            with conn.cursor() as cur:
                workspace = self.repository.get_workspace(cur, wid)
                if workspace is None:
                    return None, "not_found"
                targets = self.repository.list_workspace_targets(cur, wid)
                contents = self.repository.list_workspace_contents(cur, wid)
                return {
                    "workspace": workspace,
                    "targets": targets,
                    "contents": contents,
                }, None

    def delete_workspace(self, workspace_id: str):
        with self.db_connection_factory() as conn:
            with conn.cursor() as cur:
                if not self.repository.workspace_exists(cur, workspace_id):
                    return None, "not_found"

                deleted_contents = self.repository.count_workspace_contents(cur, workspace_id)
                vuforia_target_ids = self.repository.workspace_vuforia_target_ids(cur, workspace_id)
                target_urls = self.repository.target_upload_urls(cur, workspace_id)
                reference_urls = self.repository.target_reference_upload_urls(cur, workspace_id)
                content_urls = self.repository.content_upload_urls(cur, workspace_id)
                all_urls = list(dict.fromkeys([u for u in target_urls + reference_urls + content_urls if u]))

                deleted_vuforia_targets, vuforia_delete_errors = self._delete_vuforia_targets(vuforia_target_ids)

                file_delete_errors = []
                for url in all_urls:
                    path = self._safe_upload_abs_path_from_public_url(url)
                    if path and os.path.isfile(path):
                        try:
                            os.remove(path)
                        except OSError as exc:
                            file_delete_errors.append((path, exc))

                for url in all_urls:
                    self.repository.delete_upload_url(cur, url)

                deleted_targets = self.repository.delete_workspace_targets(cur, workspace_id)
                workspace_deleted = self.repository.delete_workspace_row(cur, workspace_id)
                return {
                    "workspaceId": workspace_id,
                    "deletedTargets": deleted_targets,
                    "deletedContents": deleted_contents,
                    "deletedUploadUrls": len(all_urls),
                    "deletedVuforiaTargets": deleted_vuforia_targets,
                    "workspaceDeleted": workspace_deleted,
                    "fileDeleteErrors": file_delete_errors,
                    "vuforiaDeleteErrors": vuforia_delete_errors,
                }, None

    def _delete_vuforia_targets(self, vuforia_target_ids: list[str]):
        deleted = 0
        errors: list[tuple[str, str]] = []
        if not vuforia_target_ids:
            return deleted, errors

        config = self.vuforia_config
        delete_fn = self.delete_vuforia_target_fn
        if config is None or not config.enabled or delete_fn is None:
            return deleted, errors

        for vuforia_id in vuforia_target_ids:
            vid = (vuforia_id or "").strip()
            if not vid:
                continue
            try:
                delete_fn(config, vid)
                deleted += 1
            except VuforiaError as exc:
                if exc.status_code == 404:
                    deleted += 1
                    continue
                errors.append((vid, str(exc)))
            except Exception as exc:
                errors.append((vid, str(exc)))

        return deleted, errors

    def _safe_upload_abs_path_from_public_url(self, url: str) -> str | None:
        if not url or not isinstance(url, str):
            return None
        base = self.public_base_url.rstrip("/")
        u = url.strip()
        if not u.startswith(base):
            return None
        tail = u[len(base) :].lstrip("/")
        if not tail.startswith("uploads/"):
            return None
        rel = tail[len("uploads/") :]
        abs_root = os.path.abspath(self.upload_folder)
        candidate = os.path.abspath(os.path.join(self.upload_folder, rel))
        if not candidate.startswith(abs_root + os.sep) and candidate != abs_root:
            return None
        return candidate

    def _resolve_workspace_thumbnail_fallback(self, cur, workspace_id: str) -> str:
        wid = (workspace_id or "").strip()
        if not wid:
            return ""

        targets = self.repository.list_workspace_targets(cur, wid)
        if not targets:
            return ""

        target_upload_root = os.path.join(self.upload_folder, "target")
        if not os.path.isdir(target_upload_root):
            return ""

        preferred_ext = [".jpg", ".jpeg", ".png", ".webp", ".gif"]
        for row in targets:
            if not row:
                continue

            existing_url = row[4] or ""
            if existing_url:
                return existing_url

            target_id = (row[0] or "").strip()
            if not target_id:
                continue

            for ext in preferred_ext:
                candidate_file = f"{target_id}{ext}"
                abs_path = os.path.join(target_upload_root, candidate_file)
                if os.path.isfile(abs_path):
                    return f"{self.public_base_url.rstrip('/')}/uploads/target/{candidate_file}"

        return ""
