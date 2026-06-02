import io
import os
import sys
from datetime import datetime, timezone
from unittest.mock import MagicMock, patch

import psycopg2
import pytest

sys.path.insert(0, os.path.dirname(__file__))
from app import app
from repositories.target_repository import TargetRepository


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


def test_api_response_includes_request_id(client):
    res = client.get("/api/nonexistent")

    assert res.status_code == 404
    assert res.headers["X-Request-Id"]


def test_api_response_uses_incoming_request_id(client):
    res = client.get("/api/nonexistent", headers={"X-Request-Id": "demo-request-id"})

    assert res.status_code == 404
    assert res.headers["X-Request-Id"] == "demo-request-id"


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
    assert body["url"].endswith("/uploads/content/test.jpg")
    assert body["fileName"] == "test.jpg"
    assert body["sizeBytes"] > 0


def test_upload_oserror_returns_500(client):
    data = {"file": (io.BytesIO(b"data"), "test.jpg")}
    with patch("werkzeug.datastructures.FileStorage.save", side_effect=OSError("disk full")):
        res = client.post("/api/upload", data=data, content_type="multipart/form-data")
    assert res.status_code == 500
    assert "Failed to save file" in res.get_json()["message"]


def test_serve_file_not_found(client):
    res = client.get("/uploads/nonexistent_file.jpg")
    assert res.status_code == 404


def test_health_success(client):
    mock_cur = MagicMock()
    mock_cur.fetchone.side_effect = [
        ("argallery",),
        (1,),
        (2,),
        (3,),
    ]

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.get("/api/health")

    assert res.status_code == 200
    body = res.get_json()
    assert body["ok"] is True
    assert body["postgresDatabase"] == "argallery"
    assert body["workspaces"] == 1
    assert body["targets"] == 2
    assert body["contents"] == 3


def test_delete_workspace_default_blocked(client):
    res = client.delete("/api/workspaces/default")

    assert res.status_code == 403
    assert res.get_json()["errorCode"] == "VALIDATION_ERROR"


def test_list_workspaces_success(client):
    mock_cur = MagicMock()
    now = datetime.now(timezone.utc)
    mock_cur.fetchall.return_value = [
        ("ws-floor-001", "Target on Floor", "ready", 1, now, now, 1, 2, "http://127.0.0.1:5050/uploads/target/floor.jpg"),
        ("ws-wall-001", "Target on Wall", "ready", 1, now, now, 1, 0, ""),
    ]

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.get("/api/workspaces")

    assert res.status_code == 200
    body = res.get_json()
    assert "workspaces" in body
    assert len(body["workspaces"]) == 2
    assert body["workspaces"][0]["workspaceId"] == "ws-floor-001"
    assert body["workspaces"][0]["targetCount"] == 1
    assert body["workspaces"][0]["contentCount"] == 2
    assert body["workspaces"][0]["thumbnailUrl"].endswith("/uploads/target/floor.jpg")


def test_get_workspace_restore_payload_success(client):
    now = datetime.now(timezone.utc)
    mock_cur = MagicMock()
    mock_cur.fetchone.return_value = ("ws-floor-001", "Target on Floor", "ready", 1, now, now)
    mock_cur.fetchall.side_effect = [
        [
            (
                "target-floor-001",
                "ws-floor-001",
                "Target on Floor",
                "Target on Floor",
                "http://127.0.0.1:5050/uploads/target/floor.jpg",
                "",
                0.2,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                1.0,
                1.0,
                1.0,
                "",
                "",
                "accepted",
                now,
                now,
            )
        ],
        [
            (
                "content-floor-001",
                "target-floor-001",
                "ws-floor-001",
                "image",
                "http://127.0.0.1:5050/uploads/content/floor-content.jpg",
                0.0,
                0.0,
                -0.5,
                0.0,
                0.0,
                0.0,
                1.0,
                1.0,
                1.0,
                "surface",
                "jpg",
                {},
                "accepted",
                now,
                now,
            )
        ],
    ]

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.get("/api/workspaces/ws-floor-001")

    assert res.status_code == 200
    body = res.get_json()
    assert body["workspace"]["workspaceId"] == "ws-floor-001"
    assert len(body["targets"]) == 1
    assert len(body["contents"]) == 1
    assert body["targets"][0]["targetId"] == "target-floor-001"
    assert body["contents"][0]["contentId"] == "content-floor-001"


def test_delete_workspace_not_found(client):
    mock_cur = MagicMock()
    mock_cur.fetchone.return_value = None

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.delete("/api/workspaces/missing")

    assert res.status_code == 404
    assert res.get_json()["errorCode"] == "NOT_FOUND"


def test_delete_workspace_success(client, tmp_path):
    app.config["UPLOAD_FOLDER"] = str(tmp_path)
    target_dir = tmp_path / "target"
    content_dir = tmp_path / "content"
    target_dir.mkdir()
    content_dir.mkdir()
    target_file = target_dir / "poster.jpg"
    content_file = content_dir / "image.jpg"
    target_file.write_bytes(b"target")
    content_file.write_bytes(b"content")

    mock_cur = MagicMock()
    mock_cur.fetchone.side_effect = [(1,), (2,)]
    mock_cur.fetchall.side_effect = [
        [("vuforia-target-abc",)],
        [("http://127.0.0.1:5050/uploads/target/poster.jpg",)],
        [],
        [("http://127.0.0.1:5050/uploads/content/image.jpg",)],
    ]
    mock_cur.rowcount = 1

    with patch("app.get_db_connection", return_value=db_context(mock_cur)), patch(
        "app.delete_vuforia_target", return_value={"targetId": "vuforia-target-abc", "resultCode": "Success"}
    ) as delete_vuforia:
        res = client.delete("/api/workspaces/demo-workspace")

    assert res.status_code == 200
    assert res.get_json() == {
        "workspaceId": "demo-workspace",
        "deletedTargets": 1,
        "deletedContents": 2,
        "deletedVuforiaTargets": 1,
    }
    delete_vuforia.assert_called_once()
    assert not target_file.exists()
    assert not content_file.exists()


def test_delete_workspace_vuforia_404_counts_as_deleted(client, tmp_path):
    from vuforia_service import VuforiaError

    app.config["UPLOAD_FOLDER"] = str(tmp_path)
    mock_cur = MagicMock()
    mock_cur.fetchone.side_effect = [(1,), (0,)]
    mock_cur.fetchall.side_effect = [
        [("missing-vu-target",)],
        [],
        [],
        [],
    ]
    mock_cur.rowcount = 1

    def _raise_404(_config, target_id):
        raise VuforiaError("not found", status_code=404)

    with patch("app.get_db_connection", return_value=db_context(mock_cur)), patch(
        "app.delete_vuforia_target", side_effect=_raise_404
    ):
        res = client.delete("/api/workspaces/demo-workspace")

    assert res.status_code == 200
    assert res.get_json()["deletedVuforiaTargets"] == 1


def test_upload_target_reference_success(client, tmp_path):
    app.config["UPLOAD_FOLDER"] = str(tmp_path)
    mock_cur = MagicMock()
    mock_cur.fetchone.side_effect = [
        (1,),
        (
            "poster-a",
            "Poster A",
            "Poster A",
            "http://127.0.0.1:5050/uploads/target/poster-a.jpg",
            "accepted",
            datetime.now(timezone.utc),
            "",
            "",
            "http://127.0.0.1:5050/uploads/target_ref/poster-a.jpg",
        ),
    ]

    data = {"file": (io.BytesIO(b"ref-bytes"), "scene.jpg")}
    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.post("/api/targets/poster-a/reference", data=data, content_type="multipart/form-data")

    assert res.status_code == 200
    body = res.get_json()
    assert body["targetReferenceImageUrl"].endswith("/uploads/target_ref/poster-a.jpg")
    assert (tmp_path / "target_ref" / "poster-a.jpg").is_file()


def test_upload_target_reference_not_found(client, tmp_path):
    app.config["UPLOAD_FOLDER"] = str(tmp_path)
    mock_cur = MagicMock()
    mock_cur.fetchone.return_value = None

    data = {"file": (io.BytesIO(b"ref-bytes"), "scene.jpg")}
    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.post("/api/targets/missing/reference", data=data, content_type="multipart/form-data")

    assert res.status_code == 404


def test_delete_vuforia_target_requires_id():
    from vuforia_service import VuforiaConfig, VuforiaError, delete_vuforia_target

    config = VuforiaConfig(access_key="key", secret_key="secret")
    with pytest.raises(VuforiaError) as exc:
        delete_vuforia_target(config, "")
    assert exc.value.status_code == 400


def test_vws_empty_body_md5_constant():
    from vuforia_service import EMPTY_BODY_MD5

    assert EMPTY_BODY_MD5 == "d41d8cd98f00b204e9800998ecf8427e"


def test_vws_bodyless_request_uses_empty_body_md5_in_signature():
    from vuforia_service import EMPTY_BODY_MD5, VuforiaConfig, _authorized_request

    config = VuforiaConfig(access_key="access", secret_key="secret")
    req = _authorized_request(config, method="DELETE", request_path="/targets/abc")
    assert req.get_header("Content-md5") == EMPTY_BODY_MD5
    assert req.get_header("Content-type") is None


def test_vws_post_request_uses_body_md5_in_signature():
    import hashlib

    from vuforia_service import VuforiaConfig, _authorized_request

    config = VuforiaConfig(access_key="access", secret_key="secret")
    body = b'{"name":"poster-a","width":0.2,"image":"abc"}'
    req = _authorized_request(config, method="POST", request_path="/targets", body=body)
    assert req.get_header("Content-md5") == hashlib.md5(body).hexdigest()
    assert req.get_header("Content-type") == "application/json"


def test_register_vuforia_target_timeout_maps_to_vuforia_error(monkeypatch):
    import urllib.error
    from unittest.mock import MagicMock

    from vuforia_service import VuforiaConfig, VuforiaError, register_vuforia_target

    config = VuforiaConfig(access_key="access", secret_key="secret")

    def _timeout(*_args, **_kwargs):
        raise TimeoutError("The read operation timed out")

    monkeypatch.setattr("vuforia_service.request.urlopen", _timeout)
    with pytest.raises(VuforiaError) as exc:
        register_vuforia_target(config, name="poster-a", image_bytes=b"123", width=0.2)
    assert exc.value.status_code == 504


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


def test_target_upsert_preserves_existing_target_image_url_when_payload_is_empty():
    repo = TargetRepository()
    mock_cur = MagicMock()
    mock_cur.fetchone.return_value = ("poster-a", "Poster A", "Main wall poster", "http://existing/image.jpg", "accepted", datetime.now(timezone.utc))

    repo.upsert_target(
        mock_cur,
        target_id="poster-a",
        workspace_id="ws-1",
        target_name="Poster A",
        display_label="Main wall poster",
        target_image_url="",
        physical_width_m=1.0,
        local_position=(0.0, 0.0, 0.0),
        local_euler=(0.0, 0.0, 0.0),
        local_scale=(1.0, 1.0, 1.0),
        meta={"schemaVersion": "v1"},
        status="accepted",
    )

    sql = mock_cur.execute.call_args[0][0]
    assert "target_image_url = CASE" in sql
    assert "WHEN EXCLUDED.target_image_url <> '' THEN EXCLUDED.target_image_url" in sql
    assert "ELSE targets.target_image_url" in sql


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




@pytest.mark.parametrize(
    "content_type,media_url,render_kind,asset_format,content_id",
    [
        pytest.param(
            "video",
            "http://example.com/demo.mp4",
            "surface",
            "mp4",
            "content-video-001",
            id="video",
        ),
        pytest.param(
            "model",
            "http://example.com/demo.glb",
            "volumetric",
            "glb",
            "content-model-001",
            id="model",
        ),
    ],
)
def test_content_create_video_and_model_success(client, content_type, media_url, render_kind, asset_format, content_id):
    mock_cur = MagicMock()
    now = datetime(2026, 4, 18, 12, 5, 1, tzinfo=timezone.utc)
    mock_cur.fetchone.side_effect = [
        (1,),
        None,
        (content_id, "poster-a", "created", now),
    ]
    payload = {
        **VALID_CONTENT_PAYLOAD,
        "contentId": content_id,
        "contentType": content_type,
        "mediaUrl": media_url,
        "renderKind": render_kind,
        "assetFormat": asset_format,
    }

    with patch("app.get_db_connection", return_value=db_context(mock_cur)):
        res = client.post("/api/content", json=payload)

    assert res.status_code == 201
    body = res.get_json()
    assert body["contentId"] == content_id
    assert body["targetId"] == "poster-a"


@pytest.mark.parametrize("content_type", ["video", "model"])
def test_content_video_and_model_require_media_url(client, content_type):
    payload = {
        **VALID_CONTENT_PAYLOAD,
        "contentId": f"content-{content_type}-missing-media",
        "contentType": content_type,
        "mediaUrl": "",
    }

    res = client.post("/api/content", json=payload)

    assert res.status_code == 400
    body = res.get_json()
    assert body["errorCode"] == "VALIDATION_ERROR"
    assert "mediaUrl" in body["message"]


def test_content_invalid_content_type(client):
    payload = {
        **VALID_CONTENT_PAYLOAD,
        "contentId": "content-invalid-type",
        "contentType": "text",
    }

    res = client.post("/api/content", json=payload)

    assert res.status_code == 400
    body = res.get_json()
    assert body["errorCode"] == "VALIDATION_ERROR"
    assert "contentType" in body["message"]

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
