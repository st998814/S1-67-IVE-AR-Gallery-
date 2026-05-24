import logging
import os
import sys
from logging.handlers import RotatingFileHandler


API_LOGGER_NAME = "argallery.api"
DB_LOGGER_NAME = "argallery.db"
VUFORIA_LOGGER_NAME = "argallery.vuforia"


def configure_logging(base_dir: str) -> None:
    """Configure AR Gallery backend logging once per process."""
    root_logger = logging.getLogger("argallery")
    if getattr(root_logger, "_argallery_configured", False):
        return

    log_level_name = os.environ.get("LOG_LEVEL", "INFO").upper()
    log_level = getattr(logging, log_level_name, logging.INFO)
    log_file = os.environ.get("LOG_FILE", os.path.join(base_dir, "server.log"))

    formatter = logging.Formatter("%(asctime)s [%(levelname)s] %(name)s: %(message)s")

    stream_handler = logging.StreamHandler(sys.stdout)
    stream_handler.setFormatter(formatter)

    file_handler = RotatingFileHandler(log_file, maxBytes=5 * 1024 * 1024, backupCount=3, encoding="utf-8")
    file_handler.setFormatter(formatter)

    root_logger.setLevel(log_level)
    root_logger.handlers.clear()
    root_logger.addHandler(stream_handler)
    root_logger.addHandler(file_handler)
    root_logger.propagate = False
    root_logger._argallery_configured = True

    for logger_name in (API_LOGGER_NAME, DB_LOGGER_NAME, VUFORIA_LOGGER_NAME):
        logger = logging.getLogger(logger_name)
        logger.setLevel(log_level)
        logger.propagate = True


def get_api_logger() -> logging.Logger:
    return logging.getLogger(API_LOGGER_NAME)


def get_db_logger() -> logging.Logger:
    return logging.getLogger(DB_LOGGER_NAME)


def get_vuforia_logger() -> logging.Logger:
    return logging.getLogger(VUFORIA_LOGGER_NAME)
