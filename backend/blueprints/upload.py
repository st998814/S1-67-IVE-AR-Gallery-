import traceback

from flask import Blueprint, current_app, jsonify, request, send_from_directory
from werkzeug.exceptions import NotFound

from api.errors import error_response
from logging_config import get_api_logger
from services.upload_service import UploadService


upload_bp = Blueprint("upload", __name__)
logger = get_api_logger()


def _service() -> UploadService:
    return UploadService(
        current_app.config["GET_DB_CONNECTION"],
        upload_folder=current_app.config["UPLOAD_FOLDER"],
        public_base_url=current_app.config["PUBLIC_BASE_URL"],
        allowed_extensions=current_app.config["ALLOWED_EXTENSIONS"],
    )


@upload_bp.route("/api/upload", methods=["POST"])
def upload_file():
    if "file" not in request.files:
        return error_response("No file part named 'file'.", "VALIDATION_ERROR", 400)

    file = request.files["file"]
    if file.filename == "":
        return error_response("No selected file.", "VALIDATION_ERROR", 400)

    ext = file.filename.rsplit(".", 1)[-1].lower() if "." in file.filename else ""
    if ext not in current_app.config["ALLOWED_EXTENSIONS"]:
        logger.warning("Upload blocked: illegal file type '%s'", ext)
        return error_response(f"File type .{ext} is not allowed.", "VALIDATION_ERROR", 415)

    try:
        result = _service().save_upload(file, request.form)
        if result["metadataError"] is not None:
            exc = result["metadataError"]
            logger.warning("Upload saved but metadata insert failed:\n%s", "".join(traceback.format_exception(type(exc), exc, exc.__traceback__)))

        logger.info("File uploaded [%s]: %s (mimetype=%s)", result["urlSegment"], result["storedFileName"], result["mimeType"])
        return jsonify(
            {
                "url": result["url"],
                "fileName": result["fileName"],
                "mimeType": result["mimeType"],
                "sizeBytes": result["sizeBytes"],
                "uploadedAtUtc": result["uploadedAtUtc"],
            }
        ), 201
    except OSError as e:
        logger.error("File save failed for '%s': %s", file.filename, e)
        return error_response("Failed to save file.", "SERVER_ERROR", 500, str(e))


@upload_bp.route("/uploads/<path:filename>")
def serve_file(filename):
    try:
        return send_from_directory(current_app.config["UPLOAD_FOLDER"], filename)
    except (FileNotFoundError, NotFound):
        logger.warning("Requested file not found: %s", filename)
        return error_response(f"File '{filename}' not found.", "NOT_FOUND", 404)
