#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────
# LedgerFlow — Health Check
# Usage: ./infra/scripts/health-check.sh
# ─────────────────────────────────────────────────────────────────────
set -euo pipefail

# Load env vars from .env if present
if [[ -f .env ]]; then
  # shellcheck disable=SC2046
  export $(grep -v '^#' .env | xargs)
fi

API_PORT="${API_PORT:-5000}"
FRONTEND_PORT="${FRONTEND_PORT:-4200}"
DB_PORT="${DB_PORT:-5432}"
REDIS_PORT="${REDIS_PORT:-6379}"
POSTGRES_USER="${POSTGRES_USER:-ledger}"
POSTGRES_DB="${POSTGRES_DB:-ledgerflow}"
REDIS_PASSWORD="${REDIS_PASSWORD:-redis_secret}"

pass() { echo "  [OK]  $1"; }
fail() { echo "  [FAIL] $1"; FAILED=1; }
FAILED=0

echo ""
echo "LedgerFlow — Health Check"
echo "================================"
echo ""

# ── PostgreSQL ────────────────────────────────
echo -n "Checking PostgreSQL... "
if docker exec ledgerflow_db pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB" -q 2>/dev/null; then
  pass "PostgreSQL is ready"
else
  fail "PostgreSQL is NOT ready"
fi

# ── Redis ─────────────────────────────────────
echo -n "Checking Redis... "
PONG=$(docker exec ledgerflow_redis redis-cli -a "$REDIS_PASSWORD" ping 2>/dev/null | tr -d '[:space:]')
if [[ "$PONG" == "PONG" ]]; then
  pass "Redis is ready"
else
  fail "Redis is NOT ready (got: $PONG)"
fi

# ── API ───────────────────────────────────────
echo -n "Checking API... "
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:${API_PORT}/health" 2>/dev/null || echo "000")
if [[ "$STATUS" == "200" ]]; then
  pass "API is ready (HTTP $STATUS)"
else
  fail "API is NOT ready (HTTP $STATUS)"
fi

# ── Frontend ──────────────────────────────────
echo -n "Checking Frontend... "
STATUS=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:${FRONTEND_PORT}/health" 2>/dev/null || echo "000")
if [[ "$STATUS" == "200" ]]; then
  pass "Frontend is ready (HTTP $STATUS)"
else
  fail "Frontend is NOT ready (HTTP $STATUS)"
fi

# ── Summary ───────────────────────────────────
echo ""
if [[ "$FAILED" -eq 0 ]]; then
  echo "All services are healthy!"
  echo ""
  echo "  Web:      http://localhost:${FRONTEND_PORT}"
  echo "  API:      http://localhost:${API_PORT}"
  echo "  Swagger:  http://localhost:${API_PORT}/swagger"
  echo "  Hangfire: http://localhost:${API_PORT}/hangfire"
  echo "  DB:       localhost:${DB_PORT}  (${POSTGRES_USER}/${POSTGRES_DB})"
  echo "  Redis:    localhost:${REDIS_PORT}"
else
  echo "Some services are unhealthy. Run 'docker compose logs' for details."
  exit 1
fi
echo ""
