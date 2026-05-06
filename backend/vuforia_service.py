import base64
import hashlib
import hmac
import json
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from email.utils import format_datetime
from typing import Optional
from urllib import error, request


@dataclass
class VuforiaConfig:
    access_key: str
    secret_key: str
    host: str = "https://vws.vuforia.com"
    target_width: float = 1.0

    @property
    def enabled(self) -> bool:
        return bool(self.access_key and self.secret_key)


class VuforiaError(RuntimeError):
    def __init__(self, message: str, status_code: Optional[int] = None, details=None):
        super().__init__(message)
        self.status_code = status_code
        self.details = details


def _http_date() -> str:
    return format_datetime(datetime.now(timezone.utc), usegmt=True)


def _sign_request(secret_key: str, method: str, content_md5: str, content_type: str, date: str, request_path: str) -> str:
    string_to_sign = "\n".join([method, content_md5, content_type, date, request_path])
    digest = hmac.new(secret_key.encode("utf-8"), string_to_sign.encode("utf-8"), hashlib.sha1).digest()
    return base64.b64encode(digest).decode("ascii")


def _authorized_request(config: VuforiaConfig, *, method: str, request_path: str, body: Optional[bytes] = None):
    content_type = "application/json" if body is not None else ""
    content_md5 = hashlib.md5(body).hexdigest() if body is not None else ""
    date = _http_date()
    signature = _sign_request(config.secret_key, method, content_md5, content_type, date, request_path)
    headers = {
        "Authorization": f"VWS {config.access_key}:{signature}",
        "Date": date,
    }
    if body is not None:
        headers["Content-Type"] = content_type
        headers["Content-MD5"] = content_md5

    return request.Request(
        config.host.rstrip("/") + request_path,
        data=body,
        method=method,
        headers=headers,
    )


def register_vuforia_target(config: VuforiaConfig, *, name: str, image_bytes: bytes, width: Optional[float] = None, metadata: Optional[dict] = None):
    if not config.enabled:
        raise VuforiaError("Vuforia credentials are not configured.", status_code=503)
    if not image_bytes:
        raise VuforiaError("Target image bytes are empty.", status_code=400)

    request_path = "/targets"
    method = "POST"
    payload = {
        "name": name,
        "width": float(width or config.target_width),
        "image": base64.b64encode(image_bytes).decode("ascii"),
        "application_metadata": base64.b64encode(json.dumps(metadata or {}).encode("utf-8")).decode("ascii"),
    }
    body = json.dumps(payload, separators=(",", ":")).encode("utf-8")
    req = _authorized_request(config, method=method, request_path=request_path, body=body)

    try:
        with request.urlopen(req, timeout=20) as resp:
            response_body = resp.read().decode("utf-8")
            parsed = json.loads(response_body) if response_body else {}
            return {
                "targetId": parsed.get("target_id"),
                "resultCode": parsed.get("result_code", "TargetCreated"),
                "transactionId": parsed.get("transaction_id"),
                "raw": parsed,
            }
    except error.HTTPError as exc:
        response_body = exc.read().decode("utf-8", errors="replace")
        try:
            details = json.loads(response_body)
        except json.JSONDecodeError:
            details = response_body
        raise VuforiaError("Vuforia target registration failed.", status_code=exc.code, details=details) from exc
    except error.URLError as exc:
        raise VuforiaError("Could not reach Vuforia Web API.", status_code=502, details=str(exc.reason)) from exc


def get_vuforia_target(config: VuforiaConfig, target_id: str):
    if not config.enabled:
        raise VuforiaError("Vuforia credentials are not configured.", status_code=503)
    if not target_id:
        raise VuforiaError("Vuforia target id is required.", status_code=400)

    request_path = f"/targets/{target_id}"
    req = _authorized_request(config, method="GET", request_path=request_path)
    try:
        with request.urlopen(req, timeout=20) as resp:
            response_body = resp.read().decode("utf-8")
            return json.loads(response_body) if response_body else {}
    except error.HTTPError as exc:
        response_body = exc.read().decode("utf-8", errors="replace")
        try:
            details = json.loads(response_body)
        except json.JSONDecodeError:
            details = response_body
        raise VuforiaError("Vuforia target status check failed.", status_code=exc.code, details=details) from exc
    except error.URLError as exc:
        raise VuforiaError("Could not reach Vuforia Web API.", status_code=502, details=str(exc.reason)) from exc


def wait_vuforia_target_ready(config: VuforiaConfig, target_id: str, *, timeout_seconds: float = 20.0, poll_interval_seconds: float = 1.5):
    deadline = time.time() + max(0.0, timeout_seconds)
    last = {}
    while time.time() <= deadline:
        info = get_vuforia_target(config, target_id)
        last = info or {}
        record = last.get("target_record") or {}
        status = str(record.get("status") or "").lower()
        active_flag = record.get("active_flag")
        if status in {"success", "active"}:
            return {"ready": True, "status": status, "raw": last}
        if status in {"failed", "failure"}:
            raise VuforiaError("Vuforia target processing failed.", status_code=502, details=last)
        if active_flag == 1 and status not in {"processing", "reprocessing"}:
            return {"ready": True, "status": status or "active", "raw": last}
        time.sleep(max(0.2, poll_interval_seconds))
    return {"ready": False, "status": "processing", "raw": last}

