#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────
# LedgerFlow — Database CRUD smoke test
# Usage: ./infra/scripts/db-test.sh
# ─────────────────────────────────────────────────────────────────────
set -euo pipefail

if [[ -f .env ]]; then
  # shellcheck disable=SC2046
  export $(grep -v '^#' .env | xargs)
fi

POSTGRES_USER="${POSTGRES_USER:-ledger}"
POSTGRES_DB="${POSTGRES_DB:-ledgerflow}"

run_sql() {
  docker exec -i ledgerflow_db psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "$1" -q 2>&1
}

echo ""
echo "Database CRUD Test"
echo "================================"

echo -n "  CREATE table...  "
run_sql "CREATE TABLE IF NOT EXISTS _smoke_test (id SERIAL PRIMARY KEY, val TEXT);" > /dev/null
echo "OK"

echo -n "  INSERT data...   "
run_sql "INSERT INTO _smoke_test (val) VALUES ('hello ledgerflow');" > /dev/null
echo "OK"

echo -n "  UPDATE data...   "
run_sql "UPDATE _smoke_test SET val = 'updated' WHERE val = 'hello ledgerflow';" > /dev/null
echo "OK"

echo -n "  SELECT data...   "
RESULT=$(run_sql "SELECT val FROM _smoke_test LIMIT 1;" | grep -E 'updated' | tr -d ' ')
[[ "$RESULT" == "updated" ]] && echo "OK" || echo "FAIL (got: $RESULT)"

echo -n "  DELETE data...   "
run_sql "DELETE FROM _smoke_test;" > /dev/null
echo "OK"

echo -n "  DROP table...    "
run_sql "DROP TABLE _smoke_test;" > /dev/null
echo "OK"

echo ""
echo "Database smoke test passed."
echo ""
