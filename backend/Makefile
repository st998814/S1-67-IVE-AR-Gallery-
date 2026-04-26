SHELL := /bin/zsh

.PHONY: db-up db-down db-logs db-reset migrate migrate-status

db-up:
	docker compose up -d db

db-down:
	docker compose down

db-logs:
	docker compose logs -f db

db-reset:
	docker compose down -v
	docker compose up -d db

migrate:
	python3 backend/scripts/migrate.py up

migrate-status:
	python3 backend/scripts/migrate.py status
