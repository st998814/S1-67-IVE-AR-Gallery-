import os
import logging
import traceback
from flask import Flask, request, jsonify, send_from_directory
from werkzeug.exceptions import NotFound
from flask_cors import CORS
import psycopg2
from werkzeug.utils import secure_filename # NEW: Helps safely name files

app = Flask(__name__)
CORS(app)

# macOS 默认常占用 5000（隔空播放接收器等），对 POST 会返回 403，改用 5050。
SERVER_PORT = 5050

# 始终使用「与 app.py 同级的 uploads」目录，不随终端当前工作目录变化
_BASE_DIR = os.path.dirname(os.path.abspath(__file__))
UPLOAD_FOLDER = os.path.join(_BASE_DIR, 'uploads')
os.makedirs(UPLOAD_FOLDER, exist_ok=True)
app.config['UPLOAD_FOLDER'] = UPLOAD_FOLDER

# ---------------------------------------------------------------------------
# Logging setup — writes to server.log AND console
# ---------------------------------------------------------------------------
LOG_FILE = os.path.join(_BASE_DIR, 'server.log')

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(message)s',
    handlers=[
        logging.FileHandler(LOG_FILE, encoding='utf-8'),
        logging.StreamHandler(),
    ]
)
logger = logging.getLogger(__name__)

# Database connection details
DB_HOST = "localhost"
DB_NAME = "ive_ar_gallery"
DB_USER = "postgres"
DB_PASS = "postgres" # Put your password back here!

def get_db_connection():
    conn = psycopg2.connect(host=DB_HOST, database=DB_NAME, user=DB_USER, password=DB_PASS)
    return conn


def _table_has_column(conn, table_name: str, column_name: str) -> bool:
    cur = conn.cursor()
    cur.execute(
        """
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = %s AND column_name = %s
        LIMIT 1
        """,
        (table_name.lower(), column_name.lower()),
    )
    ok = cur.fetchone() is not None
    cur.close()
    return ok


# ---------------------------------------------------------------------------
# Global error handlers — catch anything Flask doesn't handle
# ---------------------------------------------------------------------------
@app.errorhandler(404)
def not_found(e):
    logger.warning("404 Not Found: %s %s", request.method, request.path)
    return jsonify({"error": "Endpoint not found"}), 404


@app.errorhandler(405)
def method_not_allowed(e):
    logger.warning("405 Method Not Allowed: %s %s", request.method, request.path)
    return jsonify({"error": "Method not allowed"}), 405


@app.errorhandler(Exception)
def handle_unexpected_error(e):
    logger.error(
        "Unhandled exception on %s %s\n%s",
        request.method, request.path, traceback.format_exc()
    )
    return jsonify({"error": "Internal server error", "detail": str(e)}), 500


# --- File Upload Endpoint ---
@app.route('/api/upload', methods=['POST'])
def upload_file():
    if 'file' not in request.files:
        return jsonify({"error": "No file part"}), 400

    file = request.files['file']
    if file.filename == '':
        return jsonify({"error": "No selected file"}), 400

    try:
        filename = secure_filename(file.filename)
        save_path = os.path.join(app.config['UPLOAD_FOLDER'], filename)
        file.save(save_path)
        logger.info("File uploaded: %s", filename)

        file_url = f"http://127.0.0.1:{SERVER_PORT}/uploads/{filename}"
        return jsonify({"url": file_url}), 201
    except OSError as e:
        logger.error("File save failed for '%s': %s", file.filename, e)
        return jsonify({"error": "Failed to save file", "detail": str(e)}), 500
    except Exception as e:
        logger.error("Unexpected error in upload_file:\n%s", traceback.format_exc())
        return jsonify({"error": "Internal server error", "detail": str(e)}), 500


# --- Endpoint to let Unity download the images to view them ---
@app.route('/uploads/<filename>')
def serve_file(filename):
    try:
        return send_from_directory(app.config['UPLOAD_FOLDER'], filename)
    except (FileNotFoundError, NotFound):
        logger.warning("Requested file not found: %s", filename)
        return jsonify({"error": f"File '{filename}' not found"}), 404
    except Exception as e:
        logger.error("Unexpected error serving file '%s':\n%s", filename, traceback.format_exc())
        return jsonify({"error": "Internal server error", "detail": str(e)}), 500


# --- Database Endpoint ---
@app.route('/api/content', methods=['POST'])
def create_content():
    if not request.is_json or request.json is None:
        return jsonify({"error": "需要 application/json 请求体"}), 400

    new_data = request.json
    required = ("ContentType", "PosX", "PosY", "PosZ", "Scale", "MediaURL")
    missing = [k for k in required if k not in new_data]
    if missing:
        return jsonify({"error": f"缺少字段: {', '.join(missing)}"}), 400

    target_id = (new_data.get("TargetId") or "").strip()

    conn = None
    cur = None
    try:
        conn = get_db_connection()
        cur = conn.cursor()

        if _table_has_column(conn, "ar_content", "targetid"):
            cur.execute(
                "INSERT INTO AR_Content (ContentType, PosX, PosY, PosZ, Scale, MediaURL, TargetId) VALUES (%s, %s, %s, %s, %s, %s, %s) RETURNING id;",
                (
                    new_data["ContentType"],
                    new_data["PosX"],
                    new_data["PosY"],
                    new_data["PosZ"],
                    new_data["Scale"],
                    new_data["MediaURL"],
                    target_id,
                ),
            )
        else:
            cur.execute(
                "INSERT INTO AR_Content (ContentType, PosX, PosY, PosZ, Scale, MediaURL) VALUES (%s, %s, %s, %s, %s, %s) RETURNING id;",
                (
                    new_data["ContentType"],
                    new_data["PosX"],
                    new_data["PosY"],
                    new_data["PosZ"],
                    new_data["Scale"],
                    new_data["MediaURL"],
                ),
            )

        new_id = cur.fetchone()[0]
        conn.commit()
        logger.info("AR content created with id=%s, TargetId='%s'", new_id, target_id)
        return jsonify({"id": new_id, "message": "Content saved successfully"}), 201

    except psycopg2.Error as e:
        if conn is not None:
            conn.rollback()
        logger.error("Database error in create_content (pgcode=%s): %s", getattr(e, 'pgcode', None), e)
        return jsonify(
            {
                "error": str(e),
                "pgcode": getattr(e, "pgcode", None),
                "hint": "常见原因：表 AR_Content 不存在、列类型不匹配，或需执行 db_migrations/001_add_target_id.sql 以支持 TargetId。",
            }
        ), 500

    except Exception as e:
        if conn is not None:
            conn.rollback()
        logger.error("Unexpected error in create_content:\n%s", traceback.format_exc())
        return jsonify({"error": "Internal server error", "detail": str(e)}), 500

    finally:
        if cur is not None:
            cur.close()
        if conn is not None:
            conn.close()


if __name__ == '__main__':
    logger.info("Starting AR Gallery backend on port %s", SERVER_PORT)
    app.run(debug=True, port=SERVER_PORT)
