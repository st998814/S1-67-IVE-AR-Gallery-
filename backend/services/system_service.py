from __future__ import annotations

import os

from repositories.system_repository import SystemRepository


class SystemService:
    def __init__(self, db_connection_factory, *, public_base_url, upload_folder, repository=None):
        self.db_connection_factory = db_connection_factory
        self.public_base_url = public_base_url
        self.upload_folder = upload_folder
        self.repository = repository or SystemRepository()

    def health(self, configured_db_name: str):
        body = {"ok": True, "publicBaseUrl": self.public_base_url, "configuredDbName": configured_db_name}
        with self.db_connection_factory() as conn:
            with conn.cursor() as cur:
                body.update(self.repository.health_counts(cur))
        return body

    def delete_workspace(self, workspace_id: str):
        with self.db_connection_factory() as conn:
            with conn.cursor() as cur:
                if not self.repository.workspace_exists(cur, workspace_id):
                    return None, "not_found"

                deleted_contents = self.repository.count_workspace_contents(cur, workspace_id)
                target_urls = self.repository.target_upload_urls(cur, workspace_id)
                content_urls = self.repository.content_upload_urls(cur, workspace_id)
                all_urls = list(dict.fromkeys([u for u in target_urls + content_urls if u]))

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
                    "workspaceDeleted": workspace_deleted,
                    "fileDeleteErrors": file_delete_errors,
                }, None

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
