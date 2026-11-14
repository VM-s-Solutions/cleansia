-- DEV ONLY. Confirms the e-mail of the end-to-end test accounts (e2e.<run>.<role>@example.com), whose mail
-- SendGrid never delivers, so the E2E harness (src/Cleansia.App/apps/cleansia-platform-e2e) can sign them in.
-- Touches nothing else: no other user, no administrator, no password. The same fields ConfirmEmail clears.
-- Refuses to run on a database that does not hold the published DEV administrator, which PRO never does.
BEGIN;

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "Users" WHERE "Email" = 'admin@cleansia.local') THEN
    RAISE EXCEPTION 'dev-e2e-confirm-accounts.sql is DEV only: the published DEV administrator is missing';
  END IF;
END $$;

UPDATE "Users"
SET "IsEmailConfirmed" = TRUE,
    "ConfirmationCode" = NULL,
    "ConfirmationCodeExpiresAt" = NULL,
    "ConfirmationCodeAttempts" = 0
WHERE "Email" LIKE 'e2e.%@example.com'
  AND "Profile" <> 100
  AND NOT "IsEmailConfirmed"
RETURNING "Email" AS confirmed;

COMMIT;
