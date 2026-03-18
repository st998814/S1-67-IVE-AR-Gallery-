from flask import Flask, request, jsonify
from flask_cors import CORS
import psycopg2
from psycopg2.extras import RealDictCursor

app = Flask(__name__)
CORS(app) # Allows Unity WebGL to talk to this API

# Database connection settings
DB_CONFIG = {
    "dbname": "ive_ar_gallery",
    "user": "postgres",
    "password": "postgres", # CHANGE THIS
    "host": "localhost",
    "port": "5432"
}

def get_db_connection():
    return psycopg2.connect(**DB_CONFIG)

@app.route('/api/content', methods=['POST'])
def add_content():
    data = request.json
    conn = get_db_connection()
    cur = conn.cursor()
    cur.execute(
        "INSERT INTO AR_Content (ContentType, PosX, PosY, PosZ, Scale, MediaURL) VALUES (%s, %s, %s, %s, %s, %s) RETURNING id;",
        (data['ContentType'], data['PosX'], data['PosY'], data['PosZ'], data.get('Scale', 1.0), data.get('MediaURL', ''))
    )
    new_id = cur.fetchone()[0]
    conn.commit()
    cur.close()
    conn.close()
    return jsonify({"message": "Content added successfully", "id": new_id}), 201

@app.route('/api/content', methods=['GET'])
def get_content():
    conn = get_db_connection()
    cur = conn.cursor(cursor_factory=RealDictCursor)
    cur.execute("SELECT * FROM AR_Content;")
    content = cur.fetchall()
    cur.close()
    conn.close()
    return jsonify(content), 200

if __name__ == '__main__':
    app.run(debug=True, port=5000)