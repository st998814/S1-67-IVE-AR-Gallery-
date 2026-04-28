SHELL := /bin/zsh

.PHONY: up down logs backend-logs db-up db-down db-logs db-reset migrate migrate-status

up:
	docker compose up -d --build backend

down:
	docker compose down

logs:
	docker compose logs -f backend db

backend-logs:
	docker compose logs -f backend

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
	python3 scripts/migrate.py up

migrate-status:
	python3 scripts/migrate.py status
