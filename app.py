import os
from flask import Flask, request, jsonify, send_from_directory
from flask_cors import CORS
import psycopg2
from werkzeug.utils import secure_filename # NEW: Helps safely name files

app = Flask(__name__)
CORS(app)

# --- NEW: Set up a folder to save uploaded images ---
UPLOAD_FOLDER = 'uploads'
os.makedirs(UPLOAD_FOLDER, exist_ok=True) # Creates the folder if it doesn't exist
app.config['UPLOAD_FOLDER'] = UPLOAD_FOLDER

# Database connection details
DB_HOST = "localhost"
DB_NAME = "ive_ar_gallery"
DB_USER = "postgres"
DB_PASS = "postgres" # Put your password back here!

def get_db_connection():
    conn = psycopg2.connect(host=DB_HOST, database=DB_NAME, user=DB_USER, password=DB_PASS)
    return conn

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
        file_url = f"http://127.0.0.1:5000/uploads/{filename}"
        return jsonify({"url": file_url}), 201

# --- NEW: Endpoint to let Unity download the images to view them ---
@app.route('/uploads/<filename>')
def serve_file(filename):
    return send_from_directory(app.config['UPLOAD_FOLDER'], filename)

# --- YOUR EXISTING DATABASE ENDPOINT ---
@app.route('/api/content', methods=['POST'])
def create_content():
    new_data = request.json
    conn = get_db_connection()
    cur = conn.cursor()
    cur.execute(
        "INSERT INTO AR_Content (ContentType, PosX, PosY, PosZ, Scale, MediaURL) VALUES (%s, %s, %s, %s, %s, %s) RETURNING id;",
        (new_data['ContentType'], new_data['PosX'], new_data['PosY'], new_data['PosZ'], new_data['Scale'], new_data['MediaURL'])
    )
    new_id = cur.fetchone()[0]
    conn.commit()
    cur.close()
    conn.close()
    return jsonify({"id": new_id, "message": "Content saved successfully"}), 201

if __name__ == '__main__':
    app.run(debug=True)