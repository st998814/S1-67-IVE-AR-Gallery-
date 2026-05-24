import os
import sys
import uuid

import pytest

sys.path.insert(0, os.path.dirname(__file__))
from app import app


pytestmark = pytest.mark.skipif(
    os.environ.get("RUN_BACKEND_INTEGRATION") != "1",
    reason="Set RUN_BACKEND_INTEGRATION=1 to run tests against a real PostgreSQL database.",
)


@pytest.fixture
def client(tmp_path):
    app.config["TESTING"] = True
    app.config["UPLOAD_FOLDER"] = str(tmp_path)
    for folder in ("target", "content", "target_ref"):
        (tmp_path / folder).mkdir(exist_ok=True)
    with app.test_client() as c:
        yield c


@pytest.fixture
def integration_ids():
    suffix = uuid.uuid4().hex[:10]
    workspace_id = f"it-workspace-{suffix}"
    target_id = f"it-target-{suffix}"
    content_id = f"it-content-{suffix}"
    yield workspace_id, target_id, content_id
    with app.config["GET_DB_CONNECTION"]() as conn:
        with conn.cursor() as cur:
            cur.execute("DELETE FROM contents WHERE content_id = %s;", (content_id,))
            cur.execute("DELETE FROM targets WHERE target_id = %s;", (target_id,))
            cur.execute("DELETE FROM workspaces WHERE workspace_id = %s;", (workspace_id,))


def test_health_uses_real_database(client):
    res = client.get("/api/health")

    assert res.status_code == 200
    body = res.get_json()
    assert body["ok"] is True
    assert body["postgresDatabase"]


def test_target_content_mobileviewer_round_trip(client, integration_ids):
    workspace_id, target_id, content_id = integration_ids

    target_res = client.post(
        "/api/targets",
        json={
            "workspaceId": workspace_id,
            "workspaceName": "Integration Test Workspace",
            "targetId": target_id,
            "targetName": "Integration Target",
            "displayLabel": "Integration Label",
            "targetImageUrl": "http://example.com/integration-target.jpg",
            "physicalWidthM": 1.5,
            "localPosition": {"x": 0, "y": 0, "z": 0},
            "localEuler": {"x": 0, "y": 90, "z": 0},
            "localScale": {"x": 1, "y": 1, "z": 1},
            "meta": {"schemaVersion": "integration-test"},
        },
    )
    assert target_res.status_code == 201, target_res.get_data(as_text=True)
    assert target_res.get_json()["targetId"] == target_id

    content_res = client.post(
        "/api/content",
        json={
            "contentId": content_id,
            "targetId": target_id,
            "contentType": "image",
            "mediaUrl": "http://example.com/integration-content.jpg",
            "localPosition": {"x": 0.1, "y": 0.2, "z": 0.3},
            "localEuler": {"x": 1, "y": 2, "z": 3},
            "localScale": {"x": 1, "y": 1, "z": 1},
            "renderKind": "surface",
            "assetFormat": "jpg",
            "meta": {"title": "Integration Content", "description": "Round trip test"},
        },
    )
    assert content_res.status_code == 201, content_res.get_data(as_text=True)
    assert content_res.get_json()["contentId"] == content_id

    detail_res = client.get(f"/api/content/{content_id}")
    assert detail_res.status_code == 200
    detail = detail_res.get_json()
    assert detail["targetId"] == target_id
    assert detail["meta"]["title"] == "Integration Content"

    patch_res = client.patch(f"/api/content/{content_id}", json={"mediaUrl": "http://example.com/updated.jpg"})
    assert patch_res.status_code == 200
    assert patch_res.get_json()["status"] == "accepted"

    mobile_res = client.get(f"/api/mobileviewer/content/by-target/{target_id}")
    assert mobile_res.status_code == 200
    mobile = mobile_res.get_json()
    assert mobile["targetName"] == "Integration Target"
    assert mobile["contentType"] == "image"
    assert mobile["mediaUrl"] == "http://example.com/updated.jpg"

    delete_res = client.delete(f"/api/workspaces/{workspace_id}")
    assert delete_res.status_code == 200
    assert delete_res.get_json()["workspaceId"] == workspace_id

    missing_res = client.get(f"/api/content/{content_id}")
    assert missing_res.status_code == 404
