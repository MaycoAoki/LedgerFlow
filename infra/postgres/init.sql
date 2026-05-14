-- LedgerFlow — PostgreSQL initialisation script
-- Runs once when the container is created for the first time.

-- Enable uuid-ossp extension for UUID primary keys
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Enable pgcrypto for password hashing (if needed at DB level)
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- The application (EF Core migrations) will create the schema.
-- This file is the place for DB-level objects that EF cannot manage:
--   • extensions
--   • custom functions / triggers
--   • read-only reporting roles

-- Example reporting role (optional, uncomment if needed)
-- CREATE ROLE ledger_reader WITH LOGIN PASSWORD 'readonly_secret';
-- GRANT CONNECT ON DATABASE ledgerflow TO ledger_reader;
-- GRANT USAGE ON SCHEMA public TO ledger_reader;
-- GRANT SELECT ON ALL TABLES IN SCHEMA public TO ledger_reader;
-- ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO ledger_reader;
