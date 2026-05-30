from contextlib import contextmanager
from threading import Lock

import psycopg2
from psycopg2.pool import ThreadedConnectionPool

from config import AppConfig
from logging_config import get_db_logger


logger = get_db_logger()
_pool = None
_pool_config = None
_pool_lock = Lock()


def init_pool(config: AppConfig) -> ThreadedConnectionPool:
    global _pool, _pool_config
    with _pool_lock:
        if _pool is None:
            maxconn = max(config.db_pool_min, config.db_pool_max)
            logger.info(
                "Initializing PostgreSQL connection pool host=%s port=%s database=%s min=%s max=%s",
                config.db_host,
                config.db_port,
                config.db_name,
                config.db_pool_min,
                maxconn,
            )
            _pool = ThreadedConnectionPool(
                minconn=config.db_pool_min,
                maxconn=maxconn,
                **config.db_connect_kwargs(),
            )
        _pool_config = config
        return _pool


def configure_pool(config: AppConfig) -> None:
    global _pool_config
    _pool_config = config


def close_pool() -> None:
    global _pool
    with _pool_lock:
        if _pool is not None:
            _pool.closeall()
            _pool = None


@contextmanager
def get_db_connection():
    if _pool_config is None:
        raise RuntimeError("Database pool is not configured.")

    pool = init_pool(_pool_config)
    conn = pool.getconn()
    try:
        yield conn
        conn.commit()
    except Exception:
        conn.rollback()
        raise
    finally:
        pool.putconn(conn)


def connect_for_cli(config: AppConfig):
    return psycopg2.connect(**config.db_connect_kwargs())
