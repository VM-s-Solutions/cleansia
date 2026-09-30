-- The least-privilege login the API hosts and the Functions app connect as (E-4): it reads and writes
-- rows and nothing else — no CREATE, ALTER, DROP or TRUNCATE, so neither a SQL injection nor a leaked
-- app secret can change or drop the schema. The migration keeps the administrator login.
--
-- Run as the server administrator, connected to the application database, after every migration.
-- deploy-azure.yml does on every deploy of a stage with postgresAppLoginEnabled; by hand:
--
--   psql "host=<server>.postgres.database.azure.com dbname=Cleansia user=cleansia_admin sslmode=verify-full sslrootcert=/etc/ssl/certs/ca-certificates.crt" \
--     -v ON_ERROR_STOP=1 -v app_login=cleansia_app -v app_password=<POSTGRES_APP_PASSWORD> -f deploy/db/grant-app-login.sql
--
-- Idempotent. The first run creates the login; every run sets its password to app_password (the value
-- the hosts' Key Vault secret carries) and grants on every table and sequence that exists, and the
-- default privileges extend the same grants to whatever a later migration run by this administrator
-- creates.

\set ON_ERROR_STOP on

SELECT format('CREATE ROLE %I LOGIN', :'app_login')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'app_login') \gexec

SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L', :'app_login', :'app_password') \gexec

SELECT format('GRANT CONNECT ON DATABASE %I TO %I', current_database(), :'app_login') \gexec
SELECT format('GRANT USAGE ON SCHEMA public TO %I', :'app_login') \gexec
SELECT format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO %I', :'app_login') \gexec
SELECT format('GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO %I', :'app_login') \gexec
SELECT format('ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %I', :'app_login') \gexec
SELECT format('ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO %I', :'app_login') \gexec

-- PostgreSQL 15+ already withholds this from PUBLIC; kept so no server default can hand the login a way
-- to create objects in the schema the application reads.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
