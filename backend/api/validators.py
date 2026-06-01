import json

from flask import request

# Reference photos (placement context) — broader than Vuforia trackable images.
REFERENCE_IMAGE_EXTENSIONS = frozenset({"png", "jpg", "jpeg", "gif", "webp", "heic", "heif", "bmp"})


def _extension_allowed(ext: str, allowed: set[str]) -> bool:
    if ext not in REFERENCE_IMAGE_EXTENSIONS:
        return False
    if ext in allowed:
        return True
    return ext == "jpg" and "jpeg" in allowed


def resolve_reference_image_extension(file_storage, allowed_extensions) -> str | None:
    """Return normalized extension without dot, or None if not a supported reference image."""
    allowed = set(allowed_extensions or REFERENCE_IMAGE_EXTENSIONS)

    filename = (getattr(file_storage, "filename", None) or "").strip()
    ext = filename.rsplit(".", 1)[-1].lower() if "." in filename else ""
    if ext == "jpeg":
        ext = "jpg"
    if _extension_allowed(ext, allowed):
        return ext

    mime = (getattr(file_storage, "mimetype", None) or "").lower()
    mime_map = (
        ("image/png", "png"),
        ("image/jpeg", "jpg"),
        ("image/jpg", "jpg"),
        ("image/webp", "webp"),
        ("image/gif", "gif"),
        ("image/heic", "heic"),
        ("image/heif", "heif"),
        ("image/bmp", "bmp"),
    )
    for prefix, candidate in mime_map:
        if prefix in mime and _extension_allowed(candidate, allowed):
            return candidate

    try:
        stream = getattr(file_storage, "stream", None)
        if stream is not None:
            pos = stream.tell()
            head = stream.read(16)
            stream.seek(pos)
            if head.startswith(b"\x89PNG\r\n\x1a\n") and _extension_allowed("png", allowed):
                return "png"
            if head.startswith(b"\xff\xd8\xff") and _extension_allowed("jpg", allowed):
                return "jpg"
            if (head.startswith(b"GIF87a") or head.startswith(b"GIF89a")) and _extension_allowed("gif", allowed):
                return "gif"
            if len(head) >= 12 and head[4:8] == b"ftyp" and _extension_allowed("heic", allowed):
                return "heic"
    except Exception:
        pass

    if _extension_allowed("jpg", allowed):
        return "jpg"
    return None


def require_json_body(error_response):
    if not request.is_json:
        return None, error_response("Request body must be application/json.", "VALIDATION_ERROR", 400)
    data = request.get_json(silent=True)
    if data is None or not isinstance(data, dict):
        return None, error_response("Request body must be a JSON object.", "VALIDATION_ERROR", 400)
    return data, None


def clean_text(data: dict, field: str, required: bool = False, default: str = ""):
    value = data.get(field, default)
    if value is None:
        value = default
    if not isinstance(value, str):
        return None, f"{field} must be a string."
    value = value.strip()
    if required and not value:
        return None, f"{field} is required."
    return value, None


def parse_vector(data: dict, field: str, required: bool = True, default=None):
    if default is None:
        default = {"x": 0.0, "y": 0.0, "z": 0.0}
    value = data.get(field)
    if value is None:
        if required:
            return None, f"{field} is required."
        value = default
    if not isinstance(value, dict):
        return None, f"{field} must be an object with x, y, z."
    try:
        return (
            float(value.get("x", default["x"])),
            float(value.get("y", default["y"])),
            float(value.get("z", default["z"])),
        ), None
    except (TypeError, ValueError):
        return None, f"{field}.x/y/z must be numbers."


def parse_meta(data: dict):
    value = data.get("meta") or {}
    if not isinstance(value, dict):
        return None, "meta must be an object."
    return value, None


def parse_form_json(field: str, default):
    raw = request.form.get(field)
    if raw is None or raw == "":
        return default, None
    try:
        value = json.loads(raw)
    except json.JSONDecodeError:
        return None, f"{field} must be valid JSON."
    return value, None


def workspace_id_from_payload(data: dict, default: str = "default"):
    workspace_id = data.get("workspaceId", default)
    if workspace_id is None:
        workspace_id = default
    if not isinstance(workspace_id, str):
        return None, "workspaceId must be a string."
    workspace_id = workspace_id.strip() or default
    return workspace_id, None


def physical_width_from_payload(data: dict, default: float = 1.0):
    raw = data.get("physicalWidthM", default)
    if raw is None:
        raw = default
    try:
        value = float(raw)
    except (TypeError, ValueError):
        return None, "physicalWidthM must be a number."
    return value, None
