#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────
# LedgerFlow — Run EF Core Migrations inside the API container
# Usage: ./infra/scripts/migrate.sh [migration-name]
# ─────────────────────────────────────────────────────────────────────
set -euo pipefail

MIGRATION_NAME="${1:-}"

if [[ -n "$MIGRATION_NAME" ]]; then
  echo "Adding migration: $MIGRATION_NAME"
  docker compose exec api dotnet ef migrations add "$MIGRATION_NAME" \
    --project src/Infrastructure \
    --startup-project src/Api
else
  echo "Applying pending migrations..."
  docker compose exec api dotnet ef database update \
    --project src/Infrastructure \
    --startup-project src/Api
fi
