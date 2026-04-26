# Backend Collaboration Setup (Lightweight)

This setup keeps day-to-day backend development local (venv) while standardizing PostgreSQL with Docker.

## 1) Prepare environment

From repo root:

1. Copy `.env.example` to `.env` and adjust values if needed.
2. Export env vars in your shell (or use your preferred env loader).

Example:

```bash
cp .env.example .env
set -a
source .env
set +a
```

## 2) Start database

```bash
make db-up
```

## 3) Apply migrations

```bash
make migrate
make migrate-status
```

Migrations are tracked in the `schema_migrations` table.

## 4) Run backend

```bash
python3 backend/app.py
```

## Migration conventions

- Add new SQL files to `db_migrations/` with increasing numeric prefixes.
- Do not edit an applied migration file.
- Create a new migration for every schema change.
