# Backend Collaboration Setup

The backend now implements the v1 API contracts in `docs/api/` and stores schema baselines under `backend/db/`.

## Option A: one-command Docker startup

From `backend/`:

```bash
make up
make logs
```

This starts PostgreSQL, builds the Flask backend image, runs `python scripts/migrate.py up`, and exposes the API at `http://127.0.0.1:5050`.

To stop:

```bash
make down
```

## Option B: local Flask with Docker PostgreSQL

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
cd backend
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
cd ..
python3 backend/app.py
```

## Migration conventions

- Keep the baseline schema in `backend/db/001_init.sql`.
- Add new SQL files to `backend/db/migrations/` with increasing numeric prefixes.
- Do not edit an applied migration file.
- Create a new migration for every schema change.
