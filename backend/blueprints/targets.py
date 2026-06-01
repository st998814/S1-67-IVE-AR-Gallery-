import psycopg2
from flask import Blueprint, current_app, jsonify, request

from api.errors import error_response
from api.serializers import target_response
from api.validators import (
    clean_text,
    parse_form_json,
    parse_meta,
    parse_vector,
    physical_width_from_payload,
    require_json_body,
    resolve_reference_image_extension,
    workspace_id_from_payload,
)
from logging_config import get_api_logger
from services.target_service import TargetService
from vuforia_service import VuforiaError


targets_bp = Blueprint("targets", __name__, url_prefix="/api/targets")
logger = get_api_logger()


def _service() -> TargetService:
    return TargetService(current_app.config["GET_DB_CONNECTION"])


@targets_bp.route("", methods=["POST"])
def create_target():
    data, err = require_json_body(error_response)
    if err:
        return err

    target_id, err_msg = clean_text(data, "targetId", required=True)
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    target_name, err_msg = clean_text(data, "targetName", required=True)
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    display_label, err_msg = clean_text(data, "displayLabel", default=target_id)
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    target_image_url, err_msg = clean_text(data, "targetImageUrl")
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    target_reference_image_url, err_msg = clean_text(data, "targetReferenceImageUrl")
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    local_position, err_msg = parse_vector(data, "localPosition")
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    local_euler, err_msg = parse_vector(data, "localEuler")
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    local_scale, err_msg = parse_vector(data, "localScale", default={"x": 1.0, "y": 1.0, "z": 1.0})
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    meta, err_msg = parse_meta(data)
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    workspace_id, err_msg = workspace_id_from_payload(data)
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    workspace_name_hint, err_msg = clean_text(data, "workspaceName")
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    physical_width_m, err_msg = physical_width_from_payload(data, default=1.0)
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)

    try:
        row, status, existed = _service().create_target(
            {
                "target_id": target_id,
                "workspace_id": workspace_id,
                "workspace_name": workspace_name_hint,
                "target_name": target_name,
                "display_label": display_label,
                "target_image_url": target_image_url,
                "target_reference_image_url": target_reference_image_url,
                "physical_width_m": physical_width_m,
                "local_position": local_position,
                "local_euler": local_euler,
                "local_scale": local_scale,
                "meta": meta,
            }
        )
        return jsonify(target_response(row, status)), 201 if not existed else 200
    except psycopg2.Error as e:
        current_app.config["LOG_DATABASE_ERROR"]("create_target", e)
        return error_response("Database error while saving target.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@targets_bp.route("/cloud", methods=["POST"])
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

    local_position_raw, err_msg = parse_form_json("localPosition", {"x": 0.0, "y": 0.0, "z": 0.0})
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    local_euler_raw, err_msg = parse_form_json("localEuler", {"x": 0.0, "y": 0.0, "z": 0.0})
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    local_scale_raw, err_msg = parse_form_json("localScale", {"x": 1.0, "y": 1.0, "z": 1.0})
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    meta, err_msg = parse_form_json("meta", {"schemaVersion": "v1"})
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)

    form_vectors = {"localPosition": local_position_raw, "localEuler": local_euler_raw, "localScale": local_scale_raw}
    local_position, err_msg = parse_vector(form_vectors, "localPosition")
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    local_euler, err_msg = parse_vector(form_vectors, "localEuler")
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    local_scale, err_msg = parse_vector(form_vectors, "localScale", default={"x": 1.0, "y": 1.0, "z": 1.0})
    if err_msg:
        return error_response(err_msg, "VALIDATION_ERROR", 400)
    if not isinstance(meta, dict):
        return error_response("meta must be an object.", "VALIDATION_ERROR", 400)

    ext = file.filename.rsplit(".", 1)[-1].lower() if "." in file.filename else ""
    if ext not in {"png", "jpg", "jpeg"}:
        return error_response("Vuforia target image must be png, jpg, or jpeg.", "VALIDATION_ERROR", 415)

    try:
        row, status, existed = _service().create_cloud_target(
            file_storage=file,
            form_data={
                "target_id": target_id,
                "target_name": target_name,
                "display_label": display_label,
                "workspace_id": (request.form.get("workspaceId") or "default").strip() or "default",
                "workspace_name": (request.form.get("workspaceName") or "").strip(),
                "width": request.form.get("width"),
                "local_position": local_position,
                "local_euler": local_euler,
                "local_scale": local_scale,
                "meta": meta,
            },
            upload_folder=current_app.config["UPLOAD_FOLDER"],
            public_base_url=current_app.config["PUBLIC_BASE_URL"],
            vuforia_config=current_app.config["VUFORIA_CONFIG"],
            register_vuforia_target=current_app.config["REGISTER_VUFORIA_TARGET"],
        )
        return jsonify(target_response(row, status)), 201 if not existed else 200
    except VuforiaError as e:
        logger.warning("Vuforia target registration failed for '%s': %s", target_id, e)
        return error_response(str(e), "VUFORIA_ERROR", e.status_code or 502, e.details)
    except TimeoutError as e:
        logger.warning("Cloud target registration timed out for '%s': %s", target_id, e)
        return error_response(
            "Vuforia target registration timed out. Try a smaller image or retry.",
            "VUFORIA_TIMEOUT",
            504,
            str(e),
        )
    except OSError as e:
        if "timed out" in str(e).lower():
            logger.warning("Cloud target registration timed out for '%s': %s", target_id, e)
            return error_response(
                "Vuforia target registration timed out. Try a smaller image or retry.",
                "VUFORIA_TIMEOUT",
                504,
                str(e),
            )
        logger.error("Cloud target save failed for '%s': %s", file.filename, e)
        return error_response("Failed to save cloud target.", "SERVER_ERROR", 500, str(e))
    except ValueError as e:
        logger.error("Cloud target save failed for '%s': %s", file.filename, e)
        return error_response("Failed to save cloud target.", "SERVER_ERROR", 500, str(e))
    except psycopg2.Error as e:
        current_app.config["LOG_DATABASE_ERROR"]("create_cloud_target", e)
        return error_response("Database error while saving cloud target.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@targets_bp.route("", methods=["GET"])
def list_targets():
    try:
        return jsonify([target_response(row) for row in _service().list_targets()])
    except psycopg2.Error as e:
        current_app.config["LOG_DATABASE_ERROR"]("list_targets", e)
        return error_response("Database error while listing targets.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@targets_bp.route("/resolve", methods=["GET"])
def resolve_target_by_vuforia_id():
    vuforia_target_id = (request.args.get("vuforiaTargetId") or "").strip()
    if not vuforia_target_id:
        return error_response("vuforiaTargetId query parameter is required.", "VALIDATION_ERROR", 400)

    try:
        row = _service().resolve_by_vuforia_id(vuforia_target_id)
        if row is None:
            return error_response(f"Vuforia target '{vuforia_target_id}' was not found.", "NOT_FOUND", 404)
        return jsonify(target_response(row))
    except psycopg2.Error as e:
        current_app.config["LOG_DATABASE_ERROR"]("resolve_target_by_vuforia_id", e)
        return error_response("Database error while resolving target.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@targets_bp.route("/<path:target_id>/reference", methods=["POST"])
def upload_target_reference(target_id):
    """Upload a real-world placement reference photo; stored under uploads/target_ref/."""
    tid = (target_id or "").strip()
    if not tid:
        return error_response("targetId is required.", "VALIDATION_ERROR", 400)

    if "file" not in request.files:
        return error_response("No file part named 'file'.", "VALIDATION_ERROR", 400)

    file = request.files["file"]
    if file.filename == "":
        return error_response("No selected file.", "VALIDATION_ERROR", 400)

    resolved_ext = resolve_reference_image_extension(file, current_app.config["ALLOWED_EXTENSIONS"])
    if not resolved_ext:
        raw_ext = file.filename.rsplit(".", 1)[-1].lower() if "." in (file.filename or "") else ""
        logger.warning(
            "Target reference upload rejected for '%s': filename=%r mimetype=%r ext=%r",
            tid,
            file.filename,
            getattr(file, "mimetype", ""),
            raw_ext,
        )
        return error_response(
            f"Reference image type '.{raw_ext or '?'}' is not allowed. Use png, jpg, gif, or webp.",
            "VALIDATION_ERROR",
            415,
        )

    try:
        row = _service().upload_target_reference(
            tid,
            file,
            resolved_ext=resolved_ext,
            upload_folder=current_app.config["UPLOAD_FOLDER"],
            public_base_url=current_app.config["PUBLIC_BASE_URL"],
        )
        if row is None:
            return error_response(f"Target '{tid}' was not found.", "NOT_FOUND", 404)
        logger.info("Target reference uploaded for '%s'", tid)
        return jsonify(target_response(row, "accepted")), 200
    except ValueError as e:
        return error_response(str(e), "VALIDATION_ERROR", 400)
    except OSError as e:
        logger.error("Target reference save failed for '%s': %s", tid, e)
        return error_response("Failed to save target reference image.", "SERVER_ERROR", 500, str(e))
    except psycopg2.Error as e:
        current_app.config["LOG_DATABASE_ERROR"]("upload_target_reference", e)
        return error_response("Database error while saving target reference.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@targets_bp.route("/<path:target_id>", methods=["DELETE"])
def delete_target(target_id):
    try:
        row = _service().delete_target(target_id)
        if row is None:
            return error_response(f"Target '{target_id}' was not found.", "NOT_FOUND", 404)
        return jsonify({"targetId": row[0], "status": "deleted"})
    except psycopg2.Error as e:
        current_app.config["LOG_DATABASE_ERROR"]("delete_target", e)
        return error_response("Database error while deleting target.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})
