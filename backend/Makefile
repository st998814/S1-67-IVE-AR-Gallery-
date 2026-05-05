SHELL := /bin/zsh
COMPOSE_ENV := $(shell test -f ../.env && echo "--env-file ../.env")

.PHONY: up down logs backend-logs db-up db-down db-logs db-reset migrate migrate-status

up:
	docker compose $(COMPOSE_ENV) up -d --build backend

down:
	docker compose $(COMPOSE_ENV) down

logs:
	docker compose $(COMPOSE_ENV) logs -f backend db

backend-logs:
	docker compose $(COMPOSE_ENV) logs -f backend

db-up:
	docker compose $(COMPOSE_ENV) up -d db

db-down:
	docker compose $(COMPOSE_ENV) down

db-logs:
	docker compose $(COMPOSE_ENV) logs -f db

db-reset:
	docker compose $(COMPOSE_ENV) down -v
	docker compose $(COMPOSE_ENV) up -d db

migrate:
	python3 scripts/migrate.py up

migrate-status:
	python3 scripts/migrate.py status
