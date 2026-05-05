import base64
import hashlib
import hmac
import json
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


def register_vuforia_target(config: VuforiaConfig, *, name: str, image_bytes: bytes, width: Optional[float] = None, metadata: Optional[dict] = None):
    if not config.enabled:
        raise VuforiaError("Vuforia credentials are not configured.", status_code=503)
    if not image_bytes:
        raise VuforiaError("Target image bytes are empty.", status_code=400)

    request_path = "/targets"
    method = "POST"
    content_type = "application/json"
    payload = {
        "name": name,
        "width": float(width or config.target_width),
        "image": base64.b64encode(image_bytes).decode("ascii"),
        "application_metadata": base64.b64encode(json.dumps(metadata or {}).encode("utf-8")).decode("ascii"),
    }
    body = json.dumps(payload, separators=(",", ":")).encode("utf-8")
    content_md5 = hashlib.md5(body).hexdigest()
    date = _http_date()
    signature = _sign_request(config.secret_key, method, content_md5, content_type, date, request_path)

    req = request.Request(
        config.host.rstrip("/") + request_path,
        data=body,
        method=method,
        headers={
            "Authorization": f"VWS {config.access_key}:{signature}",
            "Content-Type": content_type,
            "Content-MD5": content_md5,
            "Date": date,
        },
    )

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

