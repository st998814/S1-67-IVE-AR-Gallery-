"""
Tests for app.py — covers exception handling and error logging (T3.22b).
Run with:  pytest backend/test_app.py -v
"""
import io
import pytest
from unittest.mock import patch, MagicMock
import psycopg2

# Import the Flask app
import sys, os
sys.path.insert(0, os.path.dirname(__file__))
from app import app


@pytest.fixture
def client():
    app.config['TESTING'] = True
    with app.test_client() as c:
        yield c


# ---------------------------------------------------------------------------
# 404 / 405 global handlers
# ---------------------------------------------------------------------------
def test_404_returns_json(client):
    res = client.get('/api/nonexistent')
    assert res.status_code == 404
    assert res.get_json()['error'] == 'Endpoint not found'


def test_405_returns_json(client):
    # /api/upload only accepts POST, so GET should be 405
    res = client.get('/api/upload')
    assert res.status_code == 405
    assert res.get_json()['error'] == 'Method not allowed'


# ---------------------------------------------------------------------------
# /api/upload
# ---------------------------------------------------------------------------
def test_upload_no_file_part(client):
    res = client.post('/api/upload')
    assert res.status_code == 400
    assert 'No file part' in res.get_json()['error']


def test_upload_empty_filename(client):
    data = {'file': (io.BytesIO(b''), '')}
    res = client.post('/api/upload', data=data, content_type='multipart/form-data')
    assert res.status_code == 400
    assert 'No selected file' in res.get_json()['error']


def test_upload_success(client, tmp_path):
    app.config['UPLOAD_FOLDER'] = str(tmp_path)
    data = {'file': (io.BytesIO(b'fake image data'), 'test.jpg')}
    res = client.post('/api/upload', data=data, content_type='multipart/form-data')
    assert res.status_code == 201
    assert 'url' in res.get_json()


def test_upload_oserror_returns_500(client):
    data = {'file': (io.BytesIO(b'data'), 'test.jpg')}
    with patch('app.secure_filename', return_value='test.jpg'), \
         patch('werkzeug.datastructures.FileStorage.save', side_effect=OSError('disk full')):
        res = client.post('/api/upload', data=data, content_type='multipart/form-data')
    assert res.status_code == 500
    assert 'Failed to save file' in res.get_json()['error']


# ---------------------------------------------------------------------------
# /uploads/<filename>
# ---------------------------------------------------------------------------
def test_serve_file_not_found(client):
    res = client.get('/uploads/nonexistent_file.jpg')
    assert res.status_code == 404


# ---------------------------------------------------------------------------
# /api/content
# ---------------------------------------------------------------------------
VALID_PAYLOAD = {
    "ContentType": "image",
    "PosX": 0.0, "PosY": 0.0, "PosZ": 0.0,
    "Scale": 1.0,
    "MediaURL": "http://example.com/img.jpg",
    "TargetId": "target_01"
}


def test_content_not_json(client):
    res = client.post('/api/content', data='not json', content_type='text/plain')
    assert res.status_code == 400
    assert '需要 application/json 请求体' in res.get_json()['error']


def test_content_missing_fields(client):
    res = client.post('/api/content', json={"ContentType": "image"})
    assert res.status_code == 400
    assert '缺少字段' in res.get_json()['error']


def test_content_db_error_returns_500(client):
    with patch('app.get_db_connection', side_effect=psycopg2.OperationalError('connection refused')):
        res = client.post('/api/content', json=VALID_PAYLOAD)
    assert res.status_code == 500
    assert 'pgcode' in res.get_json()


def test_content_unexpected_error_returns_500(client):
    with patch('app.get_db_connection', side_effect=RuntimeError('unexpected')):
        res = client.post('/api/content', json=VALID_PAYLOAD)
    assert res.status_code == 500
    assert res.get_json()['error'] == 'Internal server error'


def test_content_success(client):
    mock_conn = MagicMock()
    mock_cur = MagicMock()
    mock_conn.cursor.return_value = mock_cur
    mock_cur.fetchone.side_effect = [None, (42,)]  # _table_has_column -> no row; INSERT -> id=42

    with patch('app.get_db_connection', return_value=mock_conn):
        res = client.post('/api/content', json=VALID_PAYLOAD)

    assert res.status_code == 201
    assert res.get_json()['id'] == 42


# ---------------------------------------------------------------------------
# Logging — verify errors are actually logged
# ---------------------------------------------------------------------------
def test_db_error_is_logged(client, caplog):
    import logging
    with patch('app.get_db_connection', side_effect=psycopg2.OperationalError('fail')):
        with caplog.at_level(logging.ERROR, logger='app'):
            client.post('/api/content', json=VALID_PAYLOAD)
    assert any('Database error' in r.message for r in caplog.records)


def test_unexpected_error_is_logged(client, caplog):
    import logging
    with patch('app.get_db_connection', side_effect=RuntimeError('boom')):
        with caplog.at_level(logging.ERROR, logger='app'):
            client.post('/api/content', json=VALID_PAYLOAD)
    assert any('Unexpected error' in r.message for r in caplog.records)
