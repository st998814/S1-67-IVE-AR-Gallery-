import io
import os
import sys
from datetime import datetime, timezone
from unittest.mock import MagicMock, patch

import psycopg2
import pytest

sys.path.insert(0, os.path.dirname(__file__))
from app import app


@pytest.fixture
def client():
    app.config["TESTING"] = True
    with app.test_client() as c:
        yield c


def db_context(mock_cur):
    mock_conn = MagicMock()
    mock_conn.__enter__.return_value = mock_conn
    mock_conn.cursor.return_value.__enter__.return_value = mock_cur
    return mock_conn


def test_404_returns_json(client):
    res = client.get("/api/nonexistent")
    assert res.status_code == 404
    assert res.get_json()["errorCode"] == "NOT_FOUND"


def test_405_returns_json(client):
    res = client.get("/api/upload")
    assert res.status_code == 405
    assert res.get_json()["errorCode"] == "VALIDATION_ERROR"


def test_upload_no_file_part(client):
    res = client.post("/api/upload")
    assert res.status_code == 400
    assert "No file part" in res.get_json()["message"]


def test_upload_empty_filename(client):
    data = {"file": (io.BytesIO(b""), "")}
    res = client.post("/api/upload", data=data, content_type="multipart/form-data")
    assert res.status_code == 400
    assert "No selected file" in res.get_json()["message"]


def test_upload_success(client, tmp_path):
    app.config["UPLOAD_FOLDER"] = str(tmp_path)
    data = {"file": (io.BytesIO(b"fake image data"), "test.jpg")}
    with patch("app.get_db_connection", side_effect=psycopg2.OperationalError("db unavailable")):
        res = client.post("/api/upload", data=data, content_type="multipart/form-data")
    assert res.status_code == 201
    body = res.get_json()
    assert body["url"].endswith("/uploads/test.jpg")
    assert body["fileName"] == "test.jpg"
    assert body["sizeBytes"] > 0


def test_upload_oserror_returns_500(client):
    data = {"file": (io.BytesIO(b"data"), "test.jpg")}
    with patch("app.secure_filename", return_value="test.jpg"), \
         patch("werkzeug.datastructures.FileStorage.save", side_effect=OSError("disk full")):
        res = client.post("/api/upload", data=data, content_type="multipart/form-data")
    assert res.status_code == 500
    assert "Failed to save file" in res.get_json()["message"]


def test_serve_file_not_found(client):
    res = client.get("/uploads/nonexistent_file.jpg")
    assert res.status_code == 404


VALID_TARGET_PAYLOAD = {
    "targetId": "poster-a",
    "targetName": "Poster A",
    "displayLabel": "Main wall poster",
    "targetImageUrl": "http://example.com/poster.jpg",
    "localPosition": {"x": 0, "y": 0, "z": 0},
    "localEuler": {"x": 0, "y": 90, "z": 0},
    "localScale": {"x": 1, "y": 1, "z": 1},
    "meta": {"schemaVersion": "v1"},
}

VALID_CONTENT_PAYLOAD = {
    "contentId": "content-001",
    "targetId": "poster-a",
    "contentType": "image",
    "mediaUrl": "http://example.com/img.jpg",
    "localPosition": {"x": 0, "y": 0.1, "z": 0},
    "localEuler": {"x": 0, "y": 0, "z": 0},
    "localScale": {"x": 1, "y": 1, "z": 1},
    "renderKind": "surface",
    "assetFormat": "",
    "meta": {"schemaVersion": "v1"},
}


def content_detail_row(now):
    return (
        "content-001",
        "poster-a",
        "image",
        "http://example.com/img.jpg",
        0.0,
        0.1,
        0.0,
        0.0,
        0.0,
        0.0,
        1.0,
        1.0,
        1.0,
        "surface",
        "",
        {"schemaVersion": "v1"},
        "accepted",
        now,
        now,
    )


def test_target_success(client):
    now = datetime(2026, 4, 18, 12, 0, 1, tzinfo=timezone.utc)
    mock_cur = MagicMock()
    mock_cur.fetchone.side_effect = [
        None,
        ("poster-a", "Poster A", "Main wall poster", "http://example.com/poster.jpg", "created", now),
    ]

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.post("/api/targets", json=VALID_TARGET_PAYLOAD)

    assert res.status_code == 201
    body = res.get_json()
    assert body["targetId"] == "poster-a"
    assert body["status"] == "created"


def test_cloud_target_missing_file(client):
    res = client.post("/api/targets/cloud", data={"targetId": "poster-a"})
    assert res.status_code == 400
    assert res.get_json()["errorCode"] == "VALIDATION_ERROR"


def test_cloud_target_success(client, tmp_path):
    app.config["UPLOAD_FOLDER"] = str(tmp_path)
    now = datetime(2026, 4, 18, 12, 0, 1, tzinfo=timezone.utc)
    mock_cur = MagicMock()
    mock_cur.fetchone.side_effect = [
        None,
        ("poster-a", "Poster A", "Main wall poster", "http://127.0.0.1:5050/uploads/poster.jpg", "created", now, "vu-target", "TargetCreated"),
    ]
    data = {
        "targetId": "poster-a",
        "targetName": "Poster A",
        "displayLabel": "Main wall poster",
        "localPosition": '{"x":0,"y":0,"z":0}',
        "localEuler": '{"x":0,"y":90,"z":0}',
        "localScale": '{"x":1,"y":1,"z":1}',
        "meta": '{"schemaVersion":"v1"}',
        "file": (io.BytesIO(b"fake image data"), "poster.jpg"),
    }

    with patch("app.register_vuforia_target", return_value={"targetId": "vu-target", "resultCode": "TargetCreated"}), \
         patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.post("/api/targets/cloud", data=data, content_type="multipart/form-data")

    assert res.status_code == 201
    body = res.get_json()
    assert body["targetId"] == "poster-a"
    assert body["vuforiaTargetId"] == "vu-target"
    assert body["vuforiaStatus"] == "TargetCreated"


def test_list_targets_success(client):
    now = datetime(2026, 4, 18, 12, 0, 1, tzinfo=timezone.utc)
    mock_cur = MagicMock()
    mock_cur.fetchall.return_value = [
        ("poster-a", "Poster A", "Main wall poster", "http://example.com/poster.jpg", "accepted", now, "vu-target", "TargetCreated")
    ]

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.get("/api/targets")

    assert res.status_code == 200
    body = res.get_json()
    assert body[0]["targetId"] == "poster-a"
    assert body[0]["vuforiaTargetId"] == "vu-target"
    assert body[0]["vuforiaStatus"] == "TargetCreated"


def test_resolve_target_by_vuforia_id_success(client):
    now = datetime(2026, 4, 18, 12, 0, 1, tzinfo=timezone.utc)
    mock_cur = MagicMock()
    mock_cur.fetchone.return_value = (
        "poster-a",
        "Poster A",
        "Main wall poster",
        "http://example.com/poster.jpg",
        "accepted",
        now,
        "vu-target",
        "TargetCreated",
    )

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.get("/api/targets/resolve?vuforiaTargetId=vu-target")

    assert res.status_code == 200
    body = res.get_json()
    assert body["targetId"] == "poster-a"
    assert body["vuforiaTargetId"] == "vu-target"
    assert mock_cur.execute.call_args[0][1] == ("vu-target",)


def test_resolve_target_by_vuforia_id_missing_param(client):
    res = client.get("/api/targets/resolve")

    assert res.status_code == 400
    assert res.get_json()["errorCode"] == "VALIDATION_ERROR"


def test_resolve_target_by_vuforia_id_not_found(client):
    mock_cur = MagicMock()
    mock_cur.fetchone.return_value = None

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.get("/api/targets/resolve?vuforiaTargetId=missing")

    assert res.status_code == 404
    assert res.get_json()["errorCode"] == "NOT_FOUND"


def test_delete_target_not_found(client):
    mock_cur = MagicMock()
    mock_cur.fetchone.return_value = None

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.delete("/api/targets/missing")

    assert res.status_code == 404
    assert res.get_json()["errorCode"] == "NOT_FOUND"


def test_content_not_json(client):
    res = client.post("/api/content", data="not json", content_type="text/plain")
    assert res.status_code == 400
    assert res.get_json()["errorCode"] == "VALIDATION_ERROR"


def test_content_missing_fields(client):
    res = client.post("/api/content", json={"contentType": "image"})
    assert res.status_code == 400
    assert "contentId" in res.get_json()["message"]


def test_content_unknown_target_returns_404(client):
    mock_cur = MagicMock()
    mock_cur.fetchone.return_value = None

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.post("/api/content", json=VALID_CONTENT_PAYLOAD)

    assert res.status_code == 404
    assert res.get_json()["errorCode"] == "NOT_FOUND"


def test_content_success(client):
    mock_cur = MagicMock()
    now = datetime(2026, 4, 18, 12, 5, 1, tzinfo=timezone.utc)
    mock_cur.fetchone.side_effect = [
        (1,),
        None,
        ("content-001", "poster-a", "created", now),
    ]

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.post("/api/content", json=VALID_CONTENT_PAYLOAD)

    assert res.status_code == 201
    body = res.get_json()
    assert body["contentId"] == "content-001"
    assert body["targetId"] == "poster-a"


def test_list_content_success(client):
    mock_cur = MagicMock()
    now = datetime(2026, 4, 18, 12, 5, 1, tzinfo=timezone.utc)
    mock_cur.fetchall.return_value = [content_detail_row(now)]

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.get("/api/content")

    assert res.status_code == 200
    body = res.get_json()
    assert body[0]["contentId"] == "content-001"
    assert body[0]["localPosition"] == {"x": 0.0, "y": 0.1, "z": 0.0}


def test_list_content_filters_by_target(client):
    mock_cur = MagicMock()
    mock_cur.fetchall.return_value = []

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.get("/api/content?targetId=poster-a")

    assert res.status_code == 200
    assert res.get_json() == []
    assert mock_cur.execute.call_args[0][1] == ("poster-a",)


def test_get_content_success(client):
    mock_cur = MagicMock()
    now = datetime(2026, 4, 18, 12, 5, 1, tzinfo=timezone.utc)
    mock_cur.fetchone.return_value = content_detail_row(now)

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.get("/api/content/content-001")

    assert res.status_code == 200
    body = res.get_json()
    assert body["contentId"] == "content-001"
    assert body["contentType"] == "image"
    assert body["mediaUrl"] == "http://example.com/img.jpg"


def test_get_content_not_found(client):
    mock_cur = MagicMock()
    mock_cur.fetchone.return_value = None

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.get("/api/content/missing")

    assert res.status_code == 404
    assert res.get_json()["errorCode"] == "NOT_FOUND"


def test_patch_content_success(client):
    mock_cur = MagicMock()
    now = datetime(2026, 4, 18, 12, 5, 1, tzinfo=timezone.utc)
    mock_cur.fetchone.side_effect = [
        ("image", "http://example.com/img.jpg"),
        ("content-001", "poster-a", "accepted", now),
    ]

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.patch("/api/content/content-001", json={"mediaUrl": "http://example.com/new.jpg"})

    assert res.status_code == 200
    assert res.get_json()["status"] == "accepted"


def test_delete_content_success(client):
    mock_cur = MagicMock()
    mock_cur.fetchone.return_value = ("content-001",)

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.delete("/api/content/content-001")

    assert res.status_code == 200
    assert res.get_json() == {"contentId": "content-001", "status": "deleted"}


def test_delete_content_not_found(client):
    mock_cur = MagicMock()
    mock_cur.fetchone.return_value = None

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.delete("/api/content/missing")

    assert res.status_code == 404
    assert res.get_json()["errorCode"] == "NOT_FOUND"
