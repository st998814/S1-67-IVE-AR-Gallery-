import logging
import os
import json
import traceback
import uuid
from datetime import datetime, timezone

import psycopg2
from flask import Flask, jsonify, request, send_from_directory
from flask_cors import CORS
from psycopg2.extras import Json
from werkzeug.exceptions import HTTPException, NotFound
from werkzeug.utils import secure_filename
from vuforia_service import VuforiaConfig, VuforiaError, register_vuforia_target


def _load_local_env_file(base_dir: str):
    """Best-effort .env loader for local/dev runs."""
    env_path = os.path.join(base_dir, ".env")
    if not os.path.exists(env_path):
        return
    try:
        with open(env_path, "r", encoding="utf-8", errors="replace") as f:
            for raw in f:
                line = raw.strip()
                if not line or line.startswith("#") or "=" not in line:
                    continue
                key, value = line.split("=", 1)
                key = key.strip()
                value = value.strip().strip("\r").strip("'").strip('"')
                if key and key not in os.environ:
                    os.environ[key] = value
    except OSError:
        # Keep startup resilient; explicit env vars still take precedence.
        pass


_BASE_DIR = os.path.dirname(os.path.abspath(__file__))
_load_local_env_file(_BASE_DIR)

app = Flask(__name__)
CORS(app)

SERVER_HOST = os.environ.get("SERVER_HOST", "127.0.0.1")
SERVER_PORT = int(os.environ.get("SERVER_PORT", "5050"))
PUBLIC_BASE_URL = os.environ.get("PUBLIC_BASE_URL", f"http://127.0.0.1:{SERVER_PORT}")

UPLOAD_FOLDER = os.environ.get("UPLOAD_FOLDER", os.path.join(_BASE_DIR, "uploads"))
os.makedirs(UPLOAD_FOLDER, exist_ok=True)
app.config["UPLOAD_FOLDER"] = UPLOAD_FOLDER
UPLOAD_TARGET_FOLDER = os.path.join(UPLOAD_FOLDER, "target")
UPLOAD_CONTENT_FOLDER = os.path.join(UPLOAD_FOLDER, "content")
UPLOAD_TARGET_REF_FOLDER = os.path.join(UPLOAD_FOLDER, "target_ref")
for _d in (UPLOAD_TARGET_FOLDER, UPLOAD_CONTENT_FOLDER, UPLOAD_TARGET_REF_FOLDER):
    os.makedirs(_d, exist_ok=True)

ALLOWED_EXTENSIONS = {"png", "jpg", "jpeg", "gif", "webp", "mp4", "mov", "webm", "glb", "gltf", "txt"}
CONTENT_TYPES_REQUIRING_MEDIA = {"image", "video", "model", "model(3d)", "model3d"}

LOG_FILE = os.path.join(_BASE_DIR, "server.log")
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    handlers=[
        logging.FileHandler(LOG_FILE, encoding="utf-8"),
        logging.StreamHandler(),
    ],
)
logger = logging.getLogger(__name__)

DB_HOST = os.environ.get("DB_HOST", "localhost")
DB_PORT = int(os.environ.get("DB_PORT", "5432"))
DB_NAME = os.environ.get("DB_NAME", "ive_ar_gallery")
DB_USER = os.environ.get("DB_USER", "postgres")
DB_PASS = os.environ.get("DB_PASS", "postgres")
VUFORIA_CONFIG = VuforiaConfig(
    access_key=os.environ.get("VUFORIA_ACCESS_KEY") or os.environ.get("VUFORIA_SERVER_ACCESS_KEY", ""),
    secret_key=os.environ.get("VUFORIA_SECRET_KEY") or os.environ.get("VUFORIA_SERVER_SECRET_KEY", ""),
    host=os.environ.get("VUFORIA_HOST") or os.environ.get("VUFORIA_BASE_URL", "https://vws.vuforia.com"),
    target_width=float(os.environ.get("VUFORIA_TARGET_WIDTH", "1.0")),
)


def get_db_connection():
    return psycopg2.connect(
        host=DB_HOST,
        port=DB_PORT,
        database=DB_NAME,
        user=DB_USER,
        password=DB_PASS,
    )


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def error_response(message: str, error_code: str, status_code: int, details=None):
    body = {"message": message, "errorCode": error_code}
    if details is not None:
        body["details"] = details
    return jsonify(body), status_code


def _row_timestamp_to_iso(value) -> str:
    if value is None:
        return utc_now_iso()
    if isinstance(value, datetime):
        if value.tzinfo is None:
            value = value.replace(tzinfo=timezone.utc)
        return value.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")
    return str(value)


def _require_json_body():
    if not request.is_json:
        return None, error_response("Request body must be application/json.", "VALIDATION_ERROR", 400)
    data = request.get_json(silent=True)
    if data is None or not isinstance(data, dict):
        return None, error_response("Request body must be a JSON object.", "VALIDATION_ERROR", 400)
    return data, None


def _clean_text(data: dict, field: str, required: bool = False, default: str = ""):
    value = data.get(field, default)
    if value is None:
        value = default
    if not isinstance(value, str):
        return None, f"{field} must be a string."
    value = value.strip()
    if required and not value:
        return None, f"{field} is required."
    return value, None


def _parse_vector(data: dict, field: str, required: bool = True, default=None):
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


def _parse_meta(data: dict):
    value = data.get("meta") or {}
    if not isinstance(value, dict):
        return None, "meta must be an object."
    return value, None


def _parse_form_json(field: str, default):
    raw = request.form.get(field)
    if raw is None or raw == "":
        return default, None
    try:
        value = json.loads(raw)
    except json.JSONDecodeError:
        return None, f"{field} must be valid JSON."
    return value, None


def _target_response(row, status_override=None):
    body = {
        "targetId": row[0],
        "targetName": row[1],
        "displayLabel": row[2],
        "status": status_override or row[4],
        "createdAtUtc": _row_timestamp_to_iso(row[5]),
        "targetImageUrl": row[3],
    }
    if len(row) > 6:
        body["vuforiaTargetId"] = row[6] or ""
    if len(row) > 7:
        body["vuforiaStatus"] = row[7] or ""
    return body


def _target_cloud_response(row, status_override=None):
    body = _target_response(row, status_override)
    return body


def _workspace_id_from_payload(data: dict, default: str = "default"):
    workspace_id = data.get("workspaceId", default)
    if workspace_id is None:
        workspace_id = default
    if not isinstance(workspace_id, str):
        return None, "workspaceId must be a string."
    workspace_id = workspace_id.strip() or default
    return workspace_id, None


def _physical_width_from_payload(data: dict, default: float = 1.0):
    raw = data.get("physicalWidthM", default)
    if raw is None:
        raw = default
    try:
        value = float(raw)
    except (TypeError, ValueError):
        return None, "physicalWidthM must be a number."
    return value, None


def _content_response(row, status_override=None):
    return {
        "contentId": row[0],
        "targetId": row[1],
        "status": status_override or row[2],
        "createdAtUtc": _row_timestamp_to_iso(row[3]),
    }


def _vector_response(x, y, z):
    return {"x": float(x), "y": float(y), "z": float(z)}


def _content_detail_response(row):
    return {
        "contentId": row[0],
        "targetId": row[1],
        "contentType": row[2],
        "mediaUrl": row[3],
        "localPosition": _vector_response(row[4], row[5], row[6]),
        "localEuler": _vector_response(row[7], row[8], row[9]),
        "localScale": _vector_response(row[10], row[11], row[12]),
        "renderKind": row[13],
        "assetFormat": row[14],
        "meta": row[15] or {},
        "status": row[16],
        "createdAtUtc": _row_timestamp_to_iso(row[17]),
        "updatedAtUtc": _row_timestamp_to_iso(row[18]),
    }


CONTENT_DETAIL_SELECT = """
    SELECT
        content_id, target_id, content_type, media_url,
        local_position_x, local_position_y, local_position_z,
        local_euler_x, local_euler_y, local_euler_z,
        local_scale_x, local_scale_y, local_scale_z,
        render_kind, asset_format, meta, status, created_at_utc, updated_at_utc
    FROM contents
"""


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


def _resolve_safe_upload_filename(file_storage, upload_dir: str) -> str:
    original = secure_filename(file_storage.filename or "")
    stem, ext = os.path.splitext(original)
    if not stem:
        stem = "upload"
    ext = ext.lower()

    if not ext:
        ext = _guess_ext_from_mimetype(getattr(file_storage, "mimetype", "")) or _guess_ext_from_magic(file_storage)

    final_name = f"{stem}{ext}" if ext else stem
    candidate = final_name
    base_stem, base_ext = os.path.splitext(final_name)
    while os.path.exists(os.path.join(upload_dir, candidate)):
        candidate = f"{base_stem}-{uuid.uuid4().hex[:8]}{base_ext}"

    return candidate


@app.errorhandler(404)
def not_found(e):
    logger.warning("404 Not Found: %s %s", request.method, request.path)
    return error_response("Endpoint not found.", "NOT_FOUND", 404)


@app.errorhandler(405)
def method_not_allowed(e):
    logger.warning("405 Method Not Allowed: %s %s", request.method, request.path)
    return error_response("Method not allowed.", "VALIDATION_ERROR", 405)


@app.errorhandler(Exception)
def handle_unexpected_error(e):
    if isinstance(e, HTTPException):
        return error_response(e.description or "Request failed.", "VALIDATION_ERROR", e.code or 500)
    logger.error("Unhandled exception on %s %s\n%s", request.method, request.path, traceback.format_exc())
    return error_response("Internal server error.", "SERVER_ERROR", 500)


@app.route("/api/upload", methods=["POST"])
def upload_file():
    if "file" not in request.files:
        return error_response("No file part named 'file'.", "VALIDATION_ERROR", 400)

    file = request.files["file"]
    if file.filename == "":
        return error_response("No selected file.", "VALIDATION_ERROR", 400)

    ext = os.path.splitext(file.filename)[1].lower().replace(".", "")
    if ext not in ALLOWED_EXTENSIONS:
        logger.warning("Upload blocked: illegal file type '%s'", ext)
        return error_response(f"File type .{ext} is not allowed.", "VALIDATION_ERROR", 415)

    try:
        filename = _resolve_safe_upload_filename(file, UPLOAD_CONTENT_FOLDER)
        save_path = os.path.join(UPLOAD_CONTENT_FOLDER, filename)
        file.save(save_path)
        size_bytes = os.path.getsize(save_path)
        mime_type = getattr(file, "mimetype", "") or "application/octet-stream"
        uploaded_at = utc_now_iso()
        file_url = f"{PUBLIC_BASE_URL.rstrip('/')}/uploads/content/{filename}"

        try:
            with get_db_connection() as conn:
                with conn.cursor() as cur:
                    cur.execute(
                        """
                        INSERT INTO uploads (file_name, stored_file_name, mime_type, size_bytes, url)
                        VALUES (%s, %s, %s, %s, %s);
                        """,
                        (file.filename, filename, mime_type, size_bytes, file_url),
                    )
        except psycopg2.Error:
            logger.warning("Upload saved but metadata insert failed:\n%s", traceback.format_exc())

        logger.info("File uploaded: %s (mimetype=%s)", filename, mime_type)
        return jsonify(
            {
                "url": file_url,
                "fileName": file.filename,
                "mimeType": mime_type,
                "sizeBytes": size_bytes,
                "uploadedAtUtc": uploaded_at,
            }
        ), 201
    except OSError as e:
        logger.error("File save failed for '%s': %s", file.filename, e)
        return error_response("Failed to save file.", "SERVER_ERROR", 500, str(e))


@app.route("/uploads/<path:filename>")
def serve_file(filename):
    try:
        return send_from_directory(app.config["UPLOAD_FOLDER"], filename)
    except (FileNotFoundError, NotFound):
        logger.warning("Requested file not found: %s", filename)
        return error_response(f"File '{filename}' not found.", "NOT_FOUND", 404)


@app.route("/api/targets", methods=["POST"])
def create_target():
    data, err = _require_json_body()
    if err:
        return err

    target_id, err_msg = _clean_text(data, "targetId", required=True)
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    target_name, err_msg = _clean_text(data, "targetName", required=True)
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    display_label, err_msg = _clean_text(data, "displayLabel", default=target_id)
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    target_image_url, err_msg = _clean_text(data, "targetImageUrl")
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    local_position, err_msg = _parse_vector(data, "localPosition")
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    local_euler, err_msg = _parse_vector(data, "localEuler")
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    local_scale, err_msg = _parse_vector(data, "localScale", default={"x": 1.0, "y": 1.0, "z": 1.0})
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    meta, err_msg = _parse_meta(data)
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    workspace_id, err_msg = _workspace_id_from_payload(data)
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    physical_width_m, err_msg = _physical_width_from_payload(data, default=1.0)
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)

    try:
        with get_db_connection() as conn:
            with conn.cursor() as cur:
                cur.execute("SELECT 1 FROM targets WHERE target_id = %s;", (target_id,))
                existed = cur.fetchone() is not None
                status = "accepted" if existed else "created"
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
                row = cur.fetchone()
                return jsonify(_target_response(row, status)), 201 if not existed else 200
    except psycopg2.Error as e:
        logger.error("Database error in create_target (pgcode=%s): %s", getattr(e, "pgcode", None), e)
        return error_response("Database error while saving target.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@app.route("/api/targets/cloud", methods=["POST"])
def create_cloud_target():
    if "file" not in request.files:
        return error_response("No target image file part named 'file'.", "VALIDATION_ERROR", 400)

    file = request.files["file"]
    if file.filename == "":
        return error_response("No selected target image file.", "VALIDATION_ERROR", 400)

    target_id = (request.form.get("targetId") or "").strip()
    target_name = (request.form.get("targetName") or target_id).strip()
    display_label = (request.form.get("displayLabel") or target_id).strip()
    if not target_id:
        return error_response("targetId is required.", "VALIDATION_ERROR", 400)
    if not target_name:
        return error_response("targetName is required.", "VALIDATION_ERROR", 400)
    workspace_id = (request.form.get("workspaceId") or "default").strip() or "default"

    local_position_raw, err_msg = _parse_form_json("localPosition", {"x": 0.0, "y": 0.0, "z": 0.0})
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    local_euler_raw, err_msg = _parse_form_json("localEuler", {"x": 0.0, "y": 0.0, "z": 0.0})
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    local_scale_raw, err_msg = _parse_form_json("localScale", {"x": 1.0, "y": 1.0, "z": 1.0})
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    meta, err_msg = _parse_form_json("meta", {"schemaVersion": "v1"})
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)

    form_data = {
        "localPosition": local_position_raw,
        "localEuler": local_euler_raw,
        "localScale": local_scale_raw,
        "meta": meta,
    }
    local_position, err_msg = _parse_vector(form_data, "localPosition")
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    local_euler, err_msg = _parse_vector(form_data, "localEuler")
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    local_scale, err_msg = _parse_vector(form_data, "localScale", default={"x": 1.0, "y": 1.0, "z": 1.0})
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    if not isinstance(meta, dict):
        return error_response("meta must be an object.", "VALIDATION_ERROR", 400)

    ext = os.path.splitext(file.filename)[1].lower().replace(".", "")
    if ext not in {"png", "jpg", "jpeg"}:
        return error_response("Vuforia target image must be png, jpg, or jpeg.", "VALIDATION_ERROR", 415)

    try:
        image_bytes = file.read()
        file.stream.seek(0)
        filename = _resolve_safe_upload_filename(file, UPLOAD_TARGET_FOLDER)
        save_path = os.path.join(UPLOAD_TARGET_FOLDER, filename)
        file.save(save_path)
        file_url = f"{PUBLIC_BASE_URL.rstrip('/')}/uploads/target/{filename}"
        target_width = float(request.form.get("width") or VUFORIA_CONFIG.target_width)

        vuforia_result = register_vuforia_target(
            VUFORIA_CONFIG,
            name=target_id,
            image_bytes=image_bytes,
            width=target_width,
            metadata={"targetId": target_id, "targetName": target_name},
        )
        vuforia_target_id = vuforia_result.get("targetId") or ""
        vuforia_status = vuforia_result.get("resultCode") or "created"

        with get_db_connection() as conn:
            with conn.cursor() as cur:
                cur.execute("SELECT 1 FROM targets WHERE target_id = %s;", (target_id,))
                existed = cur.fetchone() is not None
                status = "accepted" if existed else "created"
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
                row = cur.fetchone()
                return jsonify(_target_cloud_response(row, status)), 201 if not existed else 200
    except VuforiaError as e:
        logger.warning("Vuforia target registration failed for '%s': %s", target_id, e)
        return error_response(str(e), "VUFORIA_ERROR", e.status_code or 502, e.details)
    except (OSError, ValueError) as e:
        logger.error("Cloud target save failed for '%s': %s", file.filename, e)
        return error_response("Failed to save cloud target.", "SERVER_ERROR", 500, str(e))
    except psycopg2.Error as e:
        logger.error("Database error in create_cloud_target (pgcode=%s): %s", getattr(e, "pgcode", None), e)
        return error_response("Database error while saving cloud target.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@app.route("/api/targets", methods=["GET"])
def list_targets():
    try:
        with get_db_connection() as conn:
            with conn.cursor() as cur:
                cur.execute(
                    """
                    SELECT target_id, target_name, display_label, target_image_url, status, created_at_utc,
                           vuforia_target_id, vuforia_status
                    FROM targets
                    ORDER BY created_at_utc ASC, target_id ASC;
                    """
                )
                return jsonify([_target_response(row) for row in cur.fetchall()])
    except psycopg2.Error as e:
        logger.error("Database error in list_targets (pgcode=%s): %s", getattr(e, "pgcode", None), e)
        return error_response("Database error while listing targets.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@app.route("/api/targets/resolve", methods=["GET"])
def resolve_target_by_vuforia_id():
    vuforia_target_id = (request.args.get("vuforiaTargetId") or "").strip()
    if not vuforia_target_id:
        return error_response("vuforiaTargetId query parameter is required.", "VALIDATION_ERROR", 400)

    try:
        with get_db_connection() as conn:
            with conn.cursor() as cur:
                cur.execute(
                    """
                    SELECT target_id, target_name, display_label, target_image_url, status, created_at_utc,
                           vuforia_target_id, vuforia_status
                    FROM targets
                    WHERE vuforia_target_id = %s;
                    """,
                    (vuforia_target_id,),
                )
                row = cur.fetchone()
                if row is None:
                    return error_response(f"Vuforia target '{vuforia_target_id}' was not found.", "NOT_FOUND", 404)
                return jsonify(_target_response(row))
    except psycopg2.Error as e:
        logger.error("Database error in resolve_target_by_vuforia_id (pgcode=%s): %s", getattr(e, "pgcode", None), e)
        return error_response("Database error while resolving target.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@app.route("/api/targets/<path:target_id>", methods=["DELETE"])
def delete_target(target_id):
    try:
        with get_db_connection() as conn:
            with conn.cursor() as cur:
                cur.execute("DELETE FROM targets WHERE target_id = %s RETURNING target_id;", (target_id,))
                row = cur.fetchone()
                if row is None:
                    return error_response(f"Target '{target_id}' was not found.", "NOT_FOUND", 404)
                return jsonify({"targetId": row[0], "status": "deleted"})
    except psycopg2.Error as e:
        logger.error("Database error in delete_target (pgcode=%s): %s", getattr(e, "pgcode", None), e)
        return error_response("Database error while deleting target.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@app.route("/api/content", methods=["POST"])
def create_content():
    data, err = _require_json_body()
    if err:
        return err

    content_id, err_msg = _clean_text(data, "contentId", required=True)
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    target_id, err_msg = _clean_text(data, "targetId", required=True)
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    content_type, err_msg = _clean_text(data, "contentType", required=True)
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    media_url, err_msg = _clean_text(data, "mediaUrl")
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    if content_type.lower() in CONTENT_TYPES_REQUIRING_MEDIA and not media_url:
        return error_response("mediaUrl is required for this contentType.", "VALIDATION_ERROR", 400)
    local_position, err_msg = _parse_vector(data, "localPosition")
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    local_euler, err_msg = _parse_vector(data, "localEuler")
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    local_scale, err_msg = _parse_vector(data, "localScale", default={"x": 1.0, "y": 1.0, "z": 1.0})
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    render_kind, err_msg = _clean_text(data, "renderKind")
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    asset_format, err_msg = _clean_text(data, "assetFormat")
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    meta, err_msg = _parse_meta(data)
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)

    try:
        with get_db_connection() as conn:
            with conn.cursor() as cur:
                cur.execute("SELECT 1 FROM targets WHERE target_id = %s;", (target_id,))
                if cur.fetchone() is None:
                    return error_response(f"Target '{target_id}' was not found.", "NOT_FOUND", 404)

                cur.execute("SELECT 1 FROM contents WHERE content_id = %s;", (content_id,))
                existed = cur.fetchone() is not None
                status = "accepted" if existed else "created"
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
                        Json(meta),
                        status,
                    ),
                )
                row = cur.fetchone()
                return jsonify(_content_response(row, status)), 201 if not existed else 200
    except psycopg2.Error as e:
        logger.error("Database error in create_content (pgcode=%s): %s", getattr(e, "pgcode", None), e)
        return error_response("Database error while saving content.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@app.route("/api/content", methods=["GET"])
def list_content():
    target_id = (request.args.get("targetId") or "").strip()
    try:
        with get_db_connection() as conn:
            with conn.cursor() as cur:
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
                return jsonify([_content_detail_response(row) for row in cur.fetchall()])
    except psycopg2.Error as e:
        logger.error("Database error in list_content (pgcode=%s): %s", getattr(e, "pgcode", None), e)
        return error_response("Database error while listing content.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@app.route("/api/content/<path:content_id>", methods=["GET"])
def get_content(content_id):
    try:
        with get_db_connection() as conn:
            with conn.cursor() as cur:
                cur.execute(CONTENT_DETAIL_SELECT + " WHERE content_id = %s;", (content_id,))
                row = cur.fetchone()
                if row is None:
                    return error_response(f"Content '{content_id}' was not found.", "NOT_FOUND", 404)
                return jsonify(_content_detail_response(row))
    except psycopg2.Error as e:
        logger.error("Database error in get_content (pgcode=%s): %s", getattr(e, "pgcode", None), e)
        return error_response("Database error while loading content.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@app.route("/api/content/<path:content_id>", methods=["PATCH"])
def patch_content(content_id):
    data, err = _require_json_body()
    if err:
        return err
    if "contentId" in data and data.get("contentId") != content_id:
        return error_response("contentId in body must not conflict with the path.", "VALIDATION_ERROR", 400)
    if not data:
        return error_response("Patch body must include at least one field.", "VALIDATION_ERROR", 400)

    allowed_fields = {
        "targetId",
        "contentType",
        "mediaUrl",
        "localPosition",
        "localEuler",
        "localScale",
        "renderKind",
        "assetFormat",
        "meta",
    }
    unknown = [key for key in data if key not in allowed_fields and key != "contentId"]
    if unknown:
        return error_response(f"Unsupported patch field(s): {', '.join(unknown)}.", "VALIDATION_ERROR", 400)

    updates = []
    params = []
    target_id = None
    content_type = None
    media_url = None

    if "targetId" in data:
        target_id, err_msg = _clean_text(data, "targetId", required=True)
        if err_msg:
            return error_response(err_msg, "VALIDATION_ERROR", 400)
        updates.append("target_id = %s")
        params.append(target_id)
    if "contentType" in data:
        content_type, err_msg = _clean_text(data, "contentType", required=True)
        if err_msg:
            return error_response(err_msg, "VALIDATION_ERROR", 400)
        updates.append("content_type = %s")
        params.append(content_type)
    if "mediaUrl" in data:
        media_url, err_msg = _clean_text(data, "mediaUrl")
        if err_msg:
            return error_response(err_msg, "VALIDATION_ERROR", 400)
        updates.append("media_url = %s")
        params.append(media_url)
    for api_field, columns, default in (
        ("localPosition", ("local_position_x", "local_position_y", "local_position_z"), {"x": 0.0, "y": 0.0, "z": 0.0}),
        ("localEuler", ("local_euler_x", "local_euler_y", "local_euler_z"), {"x": 0.0, "y": 0.0, "z": 0.0}),
        ("localScale", ("local_scale_x", "local_scale_y", "local_scale_z"), {"x": 1.0, "y": 1.0, "z": 1.0}),
    ):
        if api_field in data:
            vector, err_msg = _parse_vector(data, api_field, default=default)
            if err_msg:
                return error_response(err_msg, "VALIDATION_ERROR", 400)
            for column, component in zip(columns, vector):
                updates.append(f"{column} = %s")
                params.append(component)
    if "renderKind" in data:
        render_kind, err_msg = _clean_text(data, "renderKind")
        if err_msg:
            return error_response(err_msg, "VALIDATION_ERROR", 400)
        updates.append("render_kind = %s")
        params.append(render_kind)
    if "assetFormat" in data:
        asset_format, err_msg = _clean_text(data, "assetFormat")
        if err_msg:
            return error_response(err_msg, "VALIDATION_ERROR", 400)
        updates.append("asset_format = %s")
        params.append(asset_format)
    if "meta" in data:
        meta, err_msg = _parse_meta(data)
        if err_msg:
            return error_response(err_msg, "VALIDATION_ERROR", 400)
        updates.append("meta = %s")
        params.append(Json(meta))

    if not updates:
        return error_response("Patch body must include at least one patchable field.", "VALIDATION_ERROR", 400)

    try:
        with get_db_connection() as conn:
            with conn.cursor() as cur:
                cur.execute("SELECT content_type, media_url FROM contents WHERE content_id = %s;", (content_id,))
                existing = cur.fetchone()
                if existing is None:
                    return error_response(f"Content '{content_id}' was not found.", "NOT_FOUND", 404)

                final_content_type = content_type or existing[0]
                final_media_url = media_url if media_url is not None else existing[1]
                if final_content_type.lower() in CONTENT_TYPES_REQUIRING_MEDIA and not final_media_url:
                    return error_response("mediaUrl is required for this contentType.", "VALIDATION_ERROR", 400)

                if target_id is not None:
                    cur.execute("SELECT 1 FROM targets WHERE target_id = %s;", (target_id,))
                    if cur.fetchone() is None:
                        return error_response(f"Target '{target_id}' was not found.", "NOT_FOUND", 404)

                params.append(content_id)
                cur.execute(
                    f"""
                    UPDATE contents
                    SET {', '.join(updates)}, status = 'accepted', updated_at_utc = NOW()
                    WHERE content_id = %s
                    RETURNING content_id, target_id, status, created_at_utc;
                    """,
                    params,
                )
                return jsonify(_content_response(cur.fetchone(), "accepted"))
    except psycopg2.Error as e:
        logger.error("Database error in patch_content (pgcode=%s): %s", getattr(e, "pgcode", None), e)
        return error_response("Database error while updating content.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@app.route("/api/content/<path:content_id>", methods=["DELETE"])
def delete_content(content_id):
    try:
        with get_db_connection() as conn:
            with conn.cursor() as cur:
                cur.execute("DELETE FROM contents WHERE content_id = %s RETURNING content_id;", (content_id,))
                row = cur.fetchone()
                if row is None:
                    return error_response(f"Content '{content_id}' was not found.", "NOT_FOUND", 404)
                return jsonify({"contentId": row[0], "status": "deleted"})
    except psycopg2.Error as e:
        logger.error("Database error in delete_content (pgcode=%s): %s", getattr(e, "pgcode", None), e)
        return error_response("Database error while deleting content.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


if __name__ == "__main__":
    logger.info("Starting AR Gallery backend on %s:%s", SERVER_HOST, SERVER_PORT)
    app.run(debug=True, host=SERVER_HOST, port=SERVER_PORT)
