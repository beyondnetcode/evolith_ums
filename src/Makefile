.PHONY: up down build rebuild logs clean uat-up uat-reset uat-smoke uat-backup uat-status

# ── Stage UAT en kind (G-127) — delega en scripts/uat-env.sh ──────────────────
uat-up:
	../scripts/uat-env.sh up
uat-reset:
	../scripts/uat-env.sh reset
uat-smoke:
	../scripts/uat-env.sh smoke
uat-backup:
	../scripts/uat-env.sh backup
uat-status:
	../scripts/uat-env.sh status

up:
	docker compose up -d

down:
	docker compose down

build:
	docker compose build

rebuild:
	docker compose down
	docker compose build --no-cache
	docker compose up -d

logs:
	docker compose logs -f

logs-api:
	docker compose logs -f ums-api

logs-web:
	docker compose logs -f ums-web

logs-db:
	docker compose logs -f postgres

clean:
	docker compose down -v --rmi all

dev:
	docker compose -f docker-compose.yml up -d

prod:
	docker compose -f docker-compose.yml up -d --build
