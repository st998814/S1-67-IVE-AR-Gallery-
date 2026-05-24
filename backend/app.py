from __future__ import annotations

import os
import time
import traceback
import uuid

import psycopg2
from flask import Flask, g, request
from flask_cors import CORS
from werkzeug.exceptions import HTTPException
from api.errors import error_response
from blueprints.content import content_bp
from blueprints.system import system_bp
from blueprints.targets import targets_bp
from blueprints.upload import upload_bp
from config import get_config
from db_pool import configure_pool, get_db_connection
from logging_config import configure_logging, get_api_logger, get_db_logger
from vuforia_service import VuforiaConfig, register_vuforia_target


_BASE_DIR = os.path.dirname(os.path.abspath(__file__))
CONFIG = get_config(_BASE_DIR)

app = Flask(__name__)
CORS(app)

SERVER_HOST = CONFIG.server_host
SERVER_PORT = CONFIG.server_port
PUBLIC_BASE_URL = CONFIG.public_base_url

UPLOAD_FOLDER = CONFIG.upload_folder
os.makedirs(UPLOAD_FOLDER, exist_ok=True)
app.config["UPLOAD_FOLDER"] = UPLOAD_FOLDER
for _d in (
    os.path.join(UPLOAD_FOLDER, "target"),
    os.path.join(UPLOAD_FOLDER, "content"),
    os.path.join(UPLOAD_FOLDER, "target_ref"),
):
    os.makedirs(_d, exist_ok=True)

ALLOWED_EXTENSIONS = CONFIG.allowed_extensions

configure_logging(_BASE_DIR)
logger = get_api_logger()
db_logger = get_db_logger()
configure_pool(CONFIG)

DB_NAME = CONFIG.db_name
VUFORIA_CONFIG = VuforiaConfig(
    access_key=CONFIG.vuforia_access_key,
    secret_key=CONFIG.vuforia_secret_key,
    host=CONFIG.vuforia_host,
    target_width=CONFIG.vuforia_target_width,
)
app.config["PUBLIC_BASE_URL"] = PUBLIC_BASE_URL
app.config["VUFORIA_CONFIG"] = VUFORIA_CONFIG
app.config["GET_DB_CONNECTION"] = lambda: get_db_connection()
app.config["REGISTER_VUFORIA_TARGET"] = lambda *args, **kwargs: register_vuforia_target(*args, **kwargs)
app.config["ALLOWED_EXTENSIONS"] = ALLOWED_EXTENSIONS
app.config["DB_NAME"] = DB_NAME


@app.before_request
def attach_request_context():
    if not request.path.startswith("/api/"):
        return
    incoming_request_id = (request.headers.get("X-Request-Id") or "").strip()
    g.request_id = incoming_request_id or uuid.uuid4().hex
    g.request_started_at = time.perf_counter()


@app.after_request
def log_api_request(response):
    if not request.path.startswith("/api/"):
        return response

    request_id = getattr(g, "request_id", uuid.uuid4().hex)
    started_at = getattr(g, "request_started_at", None)
    duration_ms = 0.0 if started_at is None else (time.perf_counter() - started_at) * 1000.0
    response.headers["X-Request-Id"] = request_id
    logger.info(
        "api_request method=%s path=%s status=%s duration_ms=%.2f request_id=%s",
        request.method,
        request.path,
        response.status_code,
        duration_ms,
        request_id,
    )
    return response


def log_database_error(context: str, exc: psycopg2.Error) -> None:
    db_logger.error(
        "Database error in %s (pgcode=%s): %s",
        context,
        getattr(exc, "pgcode", None),
        exc,
    )


app.config["LOG_DATABASE_ERROR"] = log_database_error
app.register_blueprint(content_bp)
app.register_blueprint(targets_bp)
app.register_blueprint(upload_bp)
app.register_blueprint(system_bp)


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


if __name__ == "__main__":
    logger.info("Starting AR Gallery backend on %s:%s", SERVER_HOST, SERVER_PORT)
    app.run(debug=True, host=SERVER_HOST, port=SERVER_PORT)
