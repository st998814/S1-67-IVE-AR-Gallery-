import os
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, Set


DEFAULT_ALLOWED_EXTENSIONS = {"png", "jpg", "jpeg", "gif", "webp", "heic", "heif", "bmp", "mp4", "mov", "webm", "glb", "gltf", "txt"}


def _set_env_from_file(env_path: Path) -> None:
    if not env_path.exists():
        return
    try:
        with env_path.open("r", encoding="utf-8", errors="replace") as f:
            for raw in f:
                line = raw.strip()
                if not line or line.startswith("#") or "=" not in line:
                    continue
                key, value = line.split("=", 1)
                key = key.strip()
                value = value.strip().strip("\r").strip("'").strip('"')
                if key and key not in os.environ:
                    os.environ[key] = value
    except OSError:
        # Explicit process env vars still take precedence.
        return


def load_env(base_dir: str) -> None:
    backend_dir = Path(base_dir).resolve()
    _set_env_from_file(backend_dir.parent / ".env")
    _set_env_from_file(backend_dir / ".env")


def _env_int(name: str, default: int) -> int:
    raw = os.environ.get(name)
    if raw is None or raw == "":
        return default
    return int(raw)


def _env_float(name: str, default: float) -> float:
    raw = os.environ.get(name)
    if raw is None or raw == "":
        return default
    return float(raw)


@dataclass(frozen=True)
class AppConfig:
    base_dir: str
    server_host: str
    server_port: int
    public_base_url: str
    upload_folder: str
    allowed_extensions: Set[str] = field(default_factory=lambda: set(DEFAULT_ALLOWED_EXTENSIONS))
    db_host: str = "localhost"
    db_port: int = 5432
    db_name: str = "ive_ar_gallery"
    db_user: str = "postgres"
    db_pass: str = "postgres"
    db_pool_min: int = 1
    db_pool_max: int = 5
    vuforia_access_key: str = ""
    vuforia_secret_key: str = ""
    vuforia_host: str = "https://vws.vuforia.com"
    vuforia_target_width: float = 1.0

    def db_connect_kwargs(self) -> Dict[str, object]:
        return {
            "host": self.db_host,
            "port": self.db_port,
            "database": self.db_name,
            "user": self.db_user,
            "password": self.db_pass,
        }


def get_config(base_dir: str) -> AppConfig:
    load_env(base_dir)
    server_port = _env_int("SERVER_PORT", 5050)
    upload_folder = os.environ.get("UPLOAD_FOLDER", os.path.join(base_dir, "uploads"))
    return AppConfig(
        base_dir=base_dir,
        server_host=os.environ.get("SERVER_HOST", "127.0.0.1"),
        server_port=server_port,
        public_base_url=os.environ.get("PUBLIC_BASE_URL", f"http://127.0.0.1:{server_port}"),
        upload_folder=upload_folder,
        db_host=os.environ.get("DB_HOST", "localhost"),
        db_port=_env_int("DB_PORT", 5432),
        db_name=os.environ.get("DB_NAME", "ive_ar_gallery"),
        db_user=os.environ.get("DB_USER", "postgres"),
        db_pass=os.environ.get("DB_PASS", "postgres"),
        db_pool_min=max(1, _env_int("DB_POOL_MIN", 1)),
        db_pool_max=max(1, _env_int("DB_POOL_MAX", 5)),
        vuforia_access_key=os.environ.get("VUFORIA_ACCESS_KEY") or os.environ.get("VUFORIA_SERVER_ACCESS_KEY", ""),
        vuforia_secret_key=os.environ.get("VUFORIA_SECRET_KEY") or os.environ.get("VUFORIA_SERVER_SECRET_KEY", ""),
        vuforia_host=os.environ.get("VUFORIA_HOST") or os.environ.get("VUFORIA_BASE_URL", "https://vws.vuforia.com"),
        vuforia_target_width=_env_float("VUFORIA_TARGET_WIDTH", 1.0),
    )
