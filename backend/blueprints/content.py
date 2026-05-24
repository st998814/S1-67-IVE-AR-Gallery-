import psycopg2
from flask import Blueprint, current_app, jsonify, request
from psycopg2.extras import Json

from api.errors import error_response
from api.serializers import content_detail_response, content_response
from api.validators import clean_text, parse_meta, parse_vector, require_json_body
from services.content_service import CONTENT_TYPES_ALLOWED, CONTENT_TYPES_REQUIRING_MEDIA, ContentService


content_bp = Blueprint("content", __name__, url_prefix="/api")


def _service() -> ContentService:
    return ContentService(current_app.config["GET_DB_CONNECTION"])


@content_bp.route("/content", methods=["POST"])
def create_content():
    data, err = require_json_body(error_response)
    if err:
        return err

    parsed, err = _parse_create_content(data)
    if err:
        return err

    try:
        row, status, existed, problem = _service().create_content(parsed)
        if problem == "target_not_found":
            return error_response(f"Target '{parsed['target_id']}' was not found.", "NOT_FOUND", 404)
        return jsonify(content_response(row, status)), 201 if not existed else 200
    except psycopg2.Error as e:
        current_app.config["LOG_DATABASE_ERROR"]("create_content", e)
        return error_response("Database error while saving content.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@content_bp.route("/content", methods=["GET"])
def list_content():
    target_id = (request.args.get("targetId") or "").strip()
    try:
        return jsonify([content_detail_response(row) for row in _service().list_content(target_id)])
    except psycopg2.Error as e:
        current_app.config["LOG_DATABASE_ERROR"]("list_content", e)
        return error_response("Database error while listing content.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@content_bp.route("/mobileviewer/content/by-target/<path:target_key>", methods=["GET"])
def mobileviewer_get_content_by_target(target_key):
    target_key = (target_key or "").strip()
    if not target_key:
        return error_response("targetKey path parameter is required.", "VALIDATION_ERROR", 400)

    try:
        body, problem = _service().mobileviewer_content_by_target(
            target_key,
            public_base_url=current_app.config["PUBLIC_BASE_URL"],
            host_url=request.host_url,
        )
        if problem == "target_not_found":
            return error_response(f"Target '{target_key}' was not found.", "NOT_FOUND", 404)
        if problem == "content_not_found":
            target_id = body.get("targetId") if isinstance(body, dict) else target_key
            return error_response(f"No content configured for target '{target_id}'.", "NOT_FOUND", 404)
        return jsonify(body)
    except psycopg2.Error as e:
        current_app.config["LOG_DATABASE_ERROR"]("mobileviewer_get_content_by_target", e)
        return error_response("Database error while loading mobileviewer content.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@content_bp.route("/content/<path:content_id>", methods=["GET"])
def get_content(content_id):
    try:
        row = _service().get_content(content_id)
        if row is None:
            return error_response(f"Content '{content_id}' was not found.", "NOT_FOUND", 404)
        return jsonify(content_detail_response(row))
    except psycopg2.Error as e:
        current_app.config["LOG_DATABASE_ERROR"]("get_content", e)
        return error_response("Database error while loading content.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@content_bp.route("/content/<path:content_id>", methods=["PATCH"])
def patch_content(content_id):
    data, err = require_json_body(error_response)
    if err:
        return err
    parsed, err = _parse_patch_content(content_id, data)
    if err:
        return err

    try:
        row, problem = _service().patch_content(
            content_id,
            updates=parsed["updates"],
            params=parsed["params"],
            target_id=parsed["target_id"],
            content_type=parsed["content_type"],
            media_url=parsed["media_url"],
        )
        if problem == "content_not_found":
            return error_response(f"Content '{content_id}' was not found.", "NOT_FOUND", 404)
        if problem == "target_not_found":
            return error_response(f"Target '{parsed['target_id']}' was not found.", "NOT_FOUND", 404)
        if problem == "media_required":
            return error_response("mediaUrl is required for this contentType.", "VALIDATION_ERROR", 400)
        return jsonify(content_response(row, "accepted"))
    except psycopg2.Error as e:
        current_app.config["LOG_DATABASE_ERROR"]("patch_content", e)
        return error_response("Database error while updating content.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


@content_bp.route("/content/<path:content_id>", methods=["DELETE"])
def delete_content(content_id):
    try:
        row = _service().delete_content(content_id)
        if row is None:
            return error_response(f"Content '{content_id}' was not found.", "NOT_FOUND", 404)
        return jsonify({"contentId": row[0], "status": "deleted"})
    except psycopg2.Error as e:
        current_app.config["LOG_DATABASE_ERROR"]("delete_content", e)
        return error_response("Database error while deleting content.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})


def _parse_create_content(data):
    content_id, err_msg = clean_text(data, "contentId", required=True)
    if err_msg:
        return None, error_response(err_msg, "VALIDATION_ERROR", 400)
    target_id, err_msg = clean_text(data, "targetId", required=True)
    if err_msg:
        return None, error_response(err_msg, "VALIDATION_ERROR", 400)
    content_type, err_msg = clean_text(data, "contentType", required=True)
    if err_msg:
        return None, error_response(err_msg, "VALIDATION_ERROR", 400)
    content_type = content_type.lower()
    if content_type not in CONTENT_TYPES_ALLOWED:
        return None, error_response("contentType must be one of: image, video, model.", "VALIDATION_ERROR", 400)
    media_url, err_msg = clean_text(data, "mediaUrl")
    if err_msg:
        return None, error_response(err_msg, "VALIDATION_ERROR", 400)
    if content_type.lower() in CONTENT_TYPES_REQUIRING_MEDIA and not media_url:
        return None, error_response("mediaUrl is required for this contentType.", "VALIDATION_ERROR", 400)
    local_position, err_msg = parse_vector(data, "localPosition")
    if err_msg:
        return None, error_response(err_msg, "VALIDATION_ERROR", 400)
    local_euler, err_msg = parse_vector(data, "localEuler")
    if err_msg:
        return None, error_response(err_msg, "VALIDATION_ERROR", 400)
    local_scale, err_msg = parse_vector(data, "localScale", default={"x": 1.0, "y": 1.0, "z": 1.0})
    if err_msg:
        return None, error_response(err_msg, "VALIDATION_ERROR", 400)
    render_kind, err_msg = clean_text(data, "renderKind")
    if err_msg:
        return None, error_response(err_msg, "VALIDATION_ERROR", 400)
    asset_format, err_msg = clean_text(data, "assetFormat")
    if err_msg:
        return None, error_response(err_msg, "VALIDATION_ERROR", 400)
    meta, err_msg = parse_meta(data)
    if err_msg:
        return None, error_response(err_msg, "VALIDATION_ERROR", 400)
    return {
        "content_id": content_id,
        "target_id": target_id,
        "content_type": content_type,
        "media_url": media_url,
        "local_position": local_position,
        "local_euler": local_euler,
        "local_scale": local_scale,
        "render_kind": render_kind,
        "asset_format": asset_format,
        "meta": meta,
    }, None


def _parse_patch_content(content_id, data):
    if "contentId" in data and data.get("contentId") != content_id:
        return None, error_response("contentId in body must not conflict with the path.", "VALIDATION_ERROR", 400)
    if not data:
        return None, error_response("Patch body must include at least one field.", "VALIDATION_ERROR", 400)

    allowed_fields = {"targetId", "contentType", "mediaUrl", "localPosition", "localEuler", "localScale", "renderKind", "assetFormat", "meta"}
    unknown = [key for key in data if key not in allowed_fields and key != "contentId"]
    if unknown:
        return None, error_response(f"Unsupported patch field(s): {', '.join(unknown)}.", "VALIDATION_ERROR", 400)

    updates = []
    params = []
    target_id = None
    content_type = None
    media_url = None

    if "targetId" in data:
        target_id, err_msg = clean_text(data, "targetId", required=True)
        if err_msg:
            return None, error_response(err_msg, "VALIDATION_ERROR", 400)
        updates.append("target_id = %s")
        params.append(target_id)
    if "contentType" in data:
        content_type, err_msg = clean_text(data, "contentType", required=True)
        if err_msg:
            return None, error_response(err_msg, "VALIDATION_ERROR", 400)
        content_type = content_type.lower()
        if content_type not in CONTENT_TYPES_ALLOWED:
            return None, error_response("contentType must be one of: image, video, model.", "VALIDATION_ERROR", 400)
        updates.append("content_type = %s")
        params.append(content_type)
    if "mediaUrl" in data:
        media_url, err_msg = clean_text(data, "mediaUrl")
        if err_msg:
            return None, error_response(err_msg, "VALIDATION_ERROR", 400)
        updates.append("media_url = %s")
        params.append(media_url)
    for api_field, columns, default in (
        ("localPosition", ("local_position_x", "local_position_y", "local_position_z"), {"x": 0.0, "y": 0.0, "z": 0.0}),
        ("localEuler", ("local_euler_x", "local_euler_y", "local_euler_z"), {"x": 0.0, "y": 0.0, "z": 0.0}),
        ("localScale", ("local_scale_x", "local_scale_y", "local_scale_z"), {"x": 1.0, "y": 1.0, "z": 1.0}),
    ):
        if api_field in data:
            vector, err_msg = parse_vector(data, api_field, default=default)
            if err_msg:
                return None, error_response(err_msg, "VALIDATION_ERROR", 400)
            for column, component in zip(columns, vector):
                updates.append(f"{column} = %s")
                params.append(component)
    if "renderKind" in data:
        render_kind, err_msg = clean_text(data, "renderKind")
        if err_msg:
            return None, error_response(err_msg, "VALIDATION_ERROR", 400)
        updates.append("render_kind = %s")
        params.append(render_kind)
    if "assetFormat" in data:
        asset_format, err_msg = clean_text(data, "assetFormat")
        if err_msg:
            return None, error_response(err_msg, "VALIDATION_ERROR", 400)
        updates.append("asset_format = %s")
        params.append(asset_format)
    if "meta" in data:
        meta, err_msg = parse_meta(data)
        if err_msg:
            return None, error_response(err_msg, "VALIDATION_ERROR", 400)
        updates.append("meta = %s")
        params.append(Json(meta))
    if not updates:
        return None, error_response("Patch body must include at least one patchable field.", "VALIDATION_ERROR", 400)
    return {"updates": updates, "params": params, "target_id": target_id, "content_type": content_type, "media_url": media_url}, None
