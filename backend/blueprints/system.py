import psycopg2
from flask import Blueprint, current_app, jsonify

from api.errors import error_response
from logging_config import get_api_logger
from services.system_service import SystemService


system_bp = Blueprint("system", __name__)
logger = get_api_logger()


def _service() -> SystemService:
    return SystemService(
        current_app.config["GET_DB_CONNECTION"],
        public_base_url=current_app.config["PUBLIC_BASE_URL"],
        upload_folder=current_app.config["UPLOAD_FOLDER"],
        vuforia_config=current_app.config.get("VUFORIA_CONFIG"),
        delete_vuforia_target_fn=current_app.config.get("DELETE_VUFORIA_TARGET"),
    )


@system_bp.route("/api/health", methods=["GET"])
def health():
    try:
        return jsonify(_service().health(current_app.config["DB_NAME"])), 200
    except Exception as e:
        logger.warning("Health check database probe failed: %s", e)
        return jsonify(
            {
                "ok": False,
                "publicBaseUrl": current_app.config["PUBLIC_BASE_URL"],
                "configuredDbName": current_app.config["DB_NAME"],
                "postgresDatabase": None,
                "error": str(e),
            }
        ), 503


@system_bp.route("/api/workspaces/<path:workspace_id>", methods=["DELETE"])
def delete_workspace(workspace_id):
    wid = (workspace_id or "").strip()
    if not wid:
        return error_response("workspaceId is required.", "VALIDATION_ERROR", 400)
    if wid == "default":
        return error_response("Cannot delete the reserved workspace 'default'.", "VALIDATION_ERROR", 403)

    try:
        body, problem = _service().delete_workspace(wid)
        if problem == "not_found":
            return error_response(f"Workspace '{wid}' was not found.", "NOT_FOUND", 404)
        for path, exc in body["fileDeleteErrors"]:
            logger.warning("Workspace delete: could not remove file %s: %s", path, exc)
        for vuforia_id, exc in body.get("vuforiaDeleteErrors", []):
            logger.warning("Workspace delete: could not remove Vuforia target %s: %s", vuforia_id, exc)
        if body.get("workspaceDeleted") == 0:
            logger.warning("Workspace row missing after target deletes for workspace_id=%s", wid)
        logger.info(
            "Deleted workspace '%s' (targets=%s, contents=%s, upload_urls=%s, vuforia_targets=%s)",
            wid,
            body["deletedTargets"],
            body["deletedContents"],
            body["deletedUploadUrls"],
            body.get("deletedVuforiaTargets", 0),
        )
        return jsonify(
            {
                "workspaceId": body["workspaceId"],
                "deletedTargets": body["deletedTargets"],
                "deletedContents": body["deletedContents"],
                "deletedVuforiaTargets": body.get("deletedVuforiaTargets", 0),
            }
        ), 200
    except psycopg2.Error as e:
        current_app.config["LOG_DATABASE_ERROR"]("delete_workspace", e)
        return error_response("Database error while deleting workspace.", "SERVER_ERROR", 500, {"pgcode": getattr(e, "pgcode", None)})
