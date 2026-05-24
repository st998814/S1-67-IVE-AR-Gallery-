#!/usr/bin/env python3
import sys
from pathlib import Path


BACKEND_DIR = Path(__file__).resolve().parents[1]
DB_DIR = BACKEND_DIR / "db"
INIT_FILE = DB_DIR / "001_init.sql"
MIGRATIONS_DIR = DB_DIR / "migrations"
sys.path.insert(0, str(BACKEND_DIR))

from config import get_config
from db_pool import connect_for_cli


CONFIG = get_config(str(BACKEND_DIR))


def connect():
    return connect_for_cli(CONFIG)


def ensure_schema_migrations(cur):
    cur.execute(
        """
        CREATE TABLE IF NOT EXISTS schema_migrations (
            version TEXT PRIMARY KEY,
            applied_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
        """
    )


def read_applied(cur):
    cur.execute("SELECT version FROM schema_migrations ORDER BY version;")
    return {row[0] for row in cur.fetchall()}


def migration_files():
    files = []
    if INIT_FILE.exists():
        files.append(INIT_FILE)
    files.extend(sorted([p for p in MIGRATIONS_DIR.glob("*.sql") if p.is_file()], key=lambda p: p.name))
    return files


def apply_migration(cur, version: str, sql_text: str):
    cur.execute(sql_text)
    cur.execute("INSERT INTO schema_migrations(version) VALUES (%s);", (version,))


def cmd_status():
    with connect() as conn:
        with conn.cursor() as cur:
            ensure_schema_migrations(cur)
            applied = read_applied(cur)
            files = migration_files()
            print("Migration status:")
            for p in files:
                mark = "APPLIED" if p.name in applied else "PENDING"
                print(f"  [{mark}] {p.name}")


def cmd_up():
    files = migration_files()
    if not files:
        print(f"No migration files found in {DB_DIR}")
        return

    with connect() as conn:
        with conn.cursor() as cur:
            ensure_schema_migrations(cur)
            applied = read_applied(cur)
            pending = [p for p in files if p.name not in applied]
            if not pending:
                print("No pending migrations.")
                return

            for p in pending:
                sql_text = p.read_text(encoding="utf-8")
                print(f"Applying {p.name} ...")
                apply_migration(cur, p.name, sql_text)
            conn.commit()
            print(f"Applied {len(pending)} migration(s).")


def main():
    cmd = "up"
    if len(sys.argv) > 1:
        cmd = sys.argv[1].strip().lower()

    if cmd == "up":
        cmd_up()
        return
    if cmd == "status":
        cmd_status()
        return

    print("Usage: python backend/scripts/migrate.py [up|status]")
    sys.exit(1)


if __name__ == "__main__":
    main()
