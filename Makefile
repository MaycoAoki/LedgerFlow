.DEFAULT_GOAL := help

# ─────────────────────────────────────────────────────────────────────
# Help
# ─────────────────────────────────────────────────────────────────────
.PHONY: help
help:
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) \
		| awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-20s\033[0m %s\n", $$1, $$2}' \
		| sort

# ─────────────────────────────────────────────────────────────────────
# Infra — full stack
# ─────────────────────────────────────────────────────────────────────
.PHONY: up down restart build logs ps

up: ## Start all services (detached)
	docker compose up -d

down: ## Stop and remove all containers
	docker compose down

restart: ## Restart all services
	docker compose restart

build: ## Build all images
	docker compose build

logs: ## Follow logs (all services)
	docker compose logs -f

ps: ## Show running containers
	docker compose ps

# ─────────────────────────────────────────────────────────────────────
# Infra — dev mode (only db + redis)
# ─────────────────────────────────────────────────────────────────────
.PHONY: infra infra-down

infra: ## Start only db and redis (local API/frontend dev)
	docker compose up -d db redis

infra-down: ## Stop db and redis
	docker compose stop db redis

# ─────────────────────────────────────────────────────────────────────
# Logs per service
# ─────────────────────────────────────────────────────────────────────
.PHONY: logs-api logs-db logs-redis logs-frontend

logs-api: ## Follow API logs
	docker compose logs -f api

logs-db: ## Follow PostgreSQL logs
	docker compose logs -f db

logs-redis: ## Follow Redis logs
	docker compose logs -f redis

logs-frontend: ## Follow Frontend logs
	docker compose logs -f frontend

# ─────────────────────────────────────────────────────────────────────
# Database
# ─────────────────────────────────────────────────────────────────────
.PHONY: migrate migration db-test db-shell

migrate: ## Apply pending EF Core migrations
	./infra/scripts/migrate.sh

migration: ## Add a new migration — usage: make migration name=InitialCreate
	./infra/scripts/migrate.sh $(name)

db-test: ## Run database CRUD smoke test
	./infra/scripts/db-test.sh

db-shell: ## Open psql shell inside the db container
	docker exec -it ledgerflow_db psql -U $${POSTGRES_USER:-ledger} -d $${POSTGRES_DB:-ledgerflow}

# ─────────────────────────────────────────────────────────────────────
# Health
# ─────────────────────────────────────────────────────────────────────
.PHONY: health

health: ## Check health of all services
	./infra/scripts/health-check.sh

# ─────────────────────────────────────────────────────────────────────
# Cleanup
# ─────────────────────────────────────────────────────────────────────
.PHONY: clean nuke

clean: ## Remove stopped containers and dangling images
	docker compose down --remove-orphans
	docker image prune -f

nuke: ## Remove containers, volumes, and images (destructive!)
	docker compose down -v --remove-orphans
	docker image prune -af
