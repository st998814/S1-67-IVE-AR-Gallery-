# Backend Collaboration Setup

The backend now implements the v1 API contracts in `docs/api/` and stores schema baselines under `backend/db/`.

## Option A: one-command Docker startup

From `backend/`:

```bash
make up
make logs
```

This starts PostgreSQL, builds the Flask backend image, runs `python scripts/migrate.py up`, and exposes the API at `http://127.0.0.1:5050`.

### Same data you see in TablePlus and on disk

- **Postgres:** The `db` service is published to the host as **`localhost:${DOCKER_DB_PUBLISH:-5432}`** (default **5432**). In TablePlus, use that **host** and **port**, database **`ive_ar_gallery`**, user/password matching `docker-compose.yml`. That is the same instance the `backend` container reaches as hostname **`db`** on port **5432** inside the compose network. If you run another PostgreSQL on the host already bound to 5432, set in repo root `.env`: `DOCKER_DB_PUBLISH=5433` and connect TablePlus to **`localhost:5433`** instead.
- **Uploads:** Files are stored under **`backend/uploads/`** on your machine (bind-mounted to `/app/uploads` in the container). You should see `content/`, `target/`, etc. there after uploads—not only inside a Docker volume.

To enable Vuforia Cloud Target registration, create a local `.env` from `.env.example` at the repo root and fill:

```bash
VUFORIA_ACCESS_KEY=your_database_access_key
VUFORIA_SECRET_KEY=your_database_secret_key
VUFORIA_TARGET_WIDTH=1.0
```

For Docker, `DOCKER_DB_HOST=db` uses the bundled PostgreSQL service. To connect the backend container to a cloud PostgreSQL database instead, set `DOCKER_DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER`, and `DB_PASS` to the cloud database values.

Do not commit `.env` or share these keys publicly.

To stop:

```bash
make down
```

## Option B: local Flask with Docker PostgreSQL

When the DB container publishes a non-default host port (`DOCKER_DB_PUBLISH` in repo root `.env`), set **`DB_PORT`** in that same `.env` to the same value so `make migrate` from the host reaches Postgres.

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
