import os
from flask import Flask, request, jsonify, send_from_directory
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



# --- NEW: The File Upload Endpoint ---
@app.route('/api/upload', methods=['POST'])
def upload_file():
    if 'file' not in request.files:
        return jsonify({"error": "No file part"}), 400
    
    file = request.files['file']
    if file.filename == '':
        return jsonify({"error": "No selected file"}), 400
    
    if file:
        filename = secure_filename(file.filename)
        file.save(os.path.join(app.config['UPLOAD_FOLDER'], filename))
        
        # Create a URL that Unity can use to download the image later
        file_url = f"http://127.0.0.1:{SERVER_PORT}/uploads/{filename}"
        return jsonify({"url": file_url}), 201

# --- NEW: Endpoint to let Unity download the images to view them ---
@app.route('/uploads/<filename>')
def serve_file(filename):
    return send_from_directory(app.config['UPLOAD_FOLDER'], filename)

# --- YOUR EXISTING DATABASE ENDPOINT ---
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
        return jsonify({"id": new_id, "message": "Content saved successfully"}), 201
    except psycopg2.Error as e:
        if conn is not None:
            conn.rollback()
        return jsonify(
            {
                "error": str(e),
                "pgcode": getattr(e, "pgcode", None),
                "hint": "常见原因：表 AR_Content 不存在、列类型不匹配，或需执行 db_migrations/001_add_target_id.sql 以支持 TargetId。",
            }
        ), 500
    finally:
        if cur is not None:
            cur.close()
        if conn is not None:
            conn.close()

if __name__ == '__main__':
    app.run(debug=True, port=SERVER_PORT)