-- ============================================================
-- Retire the published-password administrator from the shared DEV database
-- ============================================================
-- Usage: Execute via GitHub Actions (execute-sql.yml), environment DEV
--   Script: fix-deactivate-local-dev-admin.sql
--
-- admin@cleansia.local / Admin123! is published in README.md. The development fixture used to create
-- it on any fresh database, so the shared DEV database can still hold it, and the DEV admin API
-- accepts a password sign-in from anywhere. Deactivating it closes that: sign-in and refresh both
-- refuse an inactive user, and an access token already issued lapses within the admin host's 15
-- minutes.
--
-- It refuses to run while that account is the only active Administrator, so DEV is never left
-- without one: create a named administrator first (README.md, "First run": administrators on the shared DEV environment). Running it again
-- is a no-op.
-- ============================================================

BEGIN;

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM "Users" WHERE "Email" = 'admin@cleansia.local' AND "IsActive")
     AND NOT EXISTS (
       SELECT 1 FROM "Users"
       WHERE "Profile" = 100 AND "AdminRole" = 1 AND "IsActive" AND "Email" <> 'admin@cleansia.local')
  THEN
    RAISE EXCEPTION 'admin@cleansia.local is the only active Administrator. Create a named administrator first (README.md), then run this script again.';
  END IF;
END $$;

UPDATE "Users"
SET "IsActive" = false,
    "UpdatedOn" = NOW(),
    "UpdatedBy" = 'admin-script'
WHERE "Email" = 'admin@cleansia.local'
  AND "IsActive";

COMMIT;

SELECT "Email", "IsActive" FROM "Users" WHERE "Email" = 'admin@cleansia.local';

SELECT COUNT(*) AS "OtherActiveAdministrators"
FROM "Users"
WHERE "Profile" = 100 AND "AdminRole" = 1 AND "IsActive" AND "Email" <> 'admin@cleansia.local';
