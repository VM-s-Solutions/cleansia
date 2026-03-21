-- ============================================
-- Reset Cleansia Database
-- Drops all tables, types, and extensions
-- so that EF Core migrations can be reapplied.
-- ============================================
-- Usage:
--   psql -h localhost -p 5432 -U postgres -d Cleansia -f reset-database.sql
--   OR run via Aspire container:
--   docker exec -i <container> psql -U postgres -d Cleansia -f - < reset-database.sql
-- ============================================

-- Drop all tables in the public schema (cascades foreign keys)
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN (
        SELECT tablename FROM pg_tables
        WHERE schemaname = 'public'
    ) LOOP
        EXECUTE 'DROP TABLE IF EXISTS public."' || r.tablename || '" CASCADE';
    END LOOP;
END $$;

-- Drop all custom enum types
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN (
        SELECT t.typname
        FROM pg_type t
        JOIN pg_namespace n ON t.typnamespace = n.oid
        WHERE n.nspname = 'public' AND t.typtype = 'e'
    ) LOOP
        EXECUTE 'DROP TYPE IF EXISTS public."' || r.typname || '" CASCADE';
    END LOOP;
END $$;

-- Drop all sequences
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN (
        SELECT sequencename FROM pg_sequences
        WHERE schemaname = 'public'
    ) LOOP
        EXECUTE 'DROP SEQUENCE IF EXISTS public."' || r.sequencename || '" CASCADE';
    END LOOP;
END $$;

-- Drop all views
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN (
        SELECT viewname FROM pg_views
        WHERE schemaname = 'public'
    ) LOOP
        EXECUTE 'DROP VIEW IF EXISTS public."' || r.viewname || '" CASCADE';
    END LOOP;
END $$;

-- Drop all functions
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN (
        SELECT p.oid::regprocedure AS func_signature
        FROM pg_proc p
        JOIN pg_namespace n ON p.pronamespace = n.oid
        WHERE n.nspname = 'public'
    ) LOOP
        EXECUTE 'DROP FUNCTION IF EXISTS ' || r.func_signature || ' CASCADE';
    END LOOP;
END $$;

-- Ensure citext extension exists (required by EF Core migrations)
CREATE EXTENSION IF NOT EXISTS citext;

-- Confirm clean state
DO $$
DECLARE
    table_count INTEGER;
BEGIN
    SELECT COUNT(*) INTO table_count FROM pg_tables WHERE schemaname = 'public';
    RAISE NOTICE 'Database reset complete. Remaining public tables: %', table_count;
END $$;
