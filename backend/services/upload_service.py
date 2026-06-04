import os
import uuid

import psycopg2
from werkzeug.utils import secure_filename

from api.serializers import utc_now_iso
from repositories.upload_repository import UploadRepository


class UploadService:
    def __init__(self, db_connection_factory, *, upload_folder, public_base_url, allowed_extensions, repository=None):
        self.db_connection_factory = db_connection_factory
        self.upload_folder = upload_folder
        self.public_base_url = public_base_url
        self.allowed_extensions = allowed_extensions
        self.repository = repository or UploadRepository()

    def save_upload(self, file_storage, form):
        raw_cat = (form.get("category") or form.get("uploadCategory") or "content").strip().lower()
        upload_dir, url_segment = self._category_dir(raw_cat)
        os.makedirs(upload_dir, exist_ok=True)

        target_id_hint = (form.get("targetId") or "").strip()
        content_id_hint = (form.get("contentId") or "").strip()
        if url_segment == "target" and target_id_hint:
            filename = self._disk_filename_for_target_image(target_id_hint, file_storage.filename)
        elif url_segment == "target_ref" and target_id_hint:
            filename = self._disk_filename_for_target_image(target_id_hint, file_storage.filename)
        elif url_segment == "content" and content_id_hint:
            filename = self._disk_filename_for_content_asset(content_id_hint, file_storage.filename)
        else:
            filename = self._resolve_safe_upload_filename(file_storage, upload_dir)

        save_path = os.path.join(upload_dir, filename)
        file_storage.save(save_path)
        size_bytes = os.path.getsize(save_path)
        mime_type = getattr(file_storage, "mimetype", "") or "application/octet-stream"
        uploaded_at = utc_now_iso()
        file_url = f"{self.public_base_url.rstrip('/')}/uploads/{url_segment}/{filename}"

        metadata_error = None
        try:
            with self.db_connection_factory() as conn:
                with conn.cursor() as cur:
                    if url_segment == "target" and target_id_hint:
                        self.repository.remove_existing_for_stored_name(cur, filename)
                    if url_segment == "content" and content_id_hint:
                        self.repository.remove_existing_for_stored_name(cur, filename)
                    self.repository.insert_upload(
                        cur,
                        file_name=file_storage.filename,
                        stored_file_name=filename,
                        mime_type=mime_type,
                        size_bytes=size_bytes,
                        url=file_url,
                    )
        except psycopg2.Error as exc:
            metadata_error = exc

        return {
            "url": file_url,
            "fileName": file_storage.filename,
            "mimeType": mime_type,
            "sizeBytes": size_bytes,
            "uploadedAtUtc": uploaded_at,
            "urlSegment": url_segment,
            "storedFileName": filename,
            "metadataError": metadata_error,
        }

    def _category_dir(self, raw_cat: str):
        if raw_cat in ("target", "target_image", "targets"):
            return os.path.join(self.upload_folder, "target"), "target"
        if raw_cat in ("target_ref", "targetref", "reference"):
            return os.path.join(self.upload_folder, "target_ref"), "target_ref"
        return os.path.join(self.upload_folder, "content"), "content"

    def _disk_filename_for_target_image(self, target_id: str, source_filename: str) -> str:
        ext = os.path.splitext(source_filename or "")[1].lower()
        if ext not in (".png", ".jpg", ".jpeg", ".gif", ".webp"):
            ext = ".jpg"
        stem = secure_filename((target_id or "").strip()) or "target"
        return f"{stem}{ext}"

    def _disk_filename_for_content_asset(self, content_id: str, source_filename: str) -> str:
        ext = os.path.splitext(source_filename or "")[1].lower()
        if not ext:
            ext = ".bin"
        ext_key = ext.lstrip(".")
        if ext_key not in self.allowed_extensions:
            ext = ".bin"
        stem = secure_filename((content_id or "").strip()) or "content"
        return f"{stem}{ext}"

    def _resolve_safe_upload_filename(self, file_storage, upload_dir: str) -> str:
        original = secure_filename(file_storage.filename or "")
        stem, ext = os.path.splitext(original)
        if not stem:
            stem = "upload"
        ext = ext.lower()
        if not ext:
            ext = self._guess_ext_from_mimetype(getattr(file_storage, "mimetype", "")) or self._guess_ext_from_magic(file_storage)

        final_name = f"{stem}{ext}" if ext else stem
        candidate = final_name
        base_stem, base_ext = os.path.splitext(final_name)
        while os.path.exists(os.path.join(upload_dir, candidate)):
            candidate = f"{base_stem}-{uuid.uuid4().hex[:8]}{base_ext}"
        return candidate

    @staticmethod
    def _guess_ext_from_mimetype(mimetype: str) -> str:
        if not mimetype:
            return ""
        lower = mimetype.lower()
        if "image/png" in lower:
            return ".png"
        if "image/jpeg" in lower or "image/jpg" in lower:
            return ".jpg"
        if "image/webp" in lower:
            return ".webp"
        if "image/gif" in lower:
            return ".gif"
        if "model/gltf-binary" in lower or "glb" in lower:
            return ".glb"
        if "video/mp4" in lower:
            return ".mp4"
        return ""

    @staticmethod
    def _guess_ext_from_magic(file_storage) -> str:
        try:
            pos = file_storage.stream.tell()
        except Exception:
            pos = None
        try:
            head = file_storage.stream.read(16)
            if pos is not None:
                file_storage.stream.seek(pos)
        except Exception:
            return ""

        if not head:
            return ""
        if head.startswith(b"\x89PNG\r\n\x1a\n"):
            return ".png"
        if head.startswith(b"\xff\xd8\xff"):
            return ".jpg"
        if head.startswith(b"GIF87a") or head.startswith(b"GIF89a"):
            return ".gif"
        if len(head) >= 4 and head[0:4] == b"glTF":
            return ".glb"
        return ""
