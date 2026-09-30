-- ============================================================
-- LOCAL DEVELOPMENT ADMINISTRATOR — never for a shared database
-- ============================================================
--
--     admin@cleansia.local  /  Admin123!
--
-- The password is published in README.md, so this account must exist only on a developer's own
-- database. A local Development boot runs this file right after prod-bootstrap.sql and
-- insert_seed_data.sql (DatabaseMigrationExtensions.DevelopmentSeedScripts), and only when that
-- boot found an empty Languages table. execute-sql.yml refuses this file for DEV and PRO alike: the
-- shared DEV database gets named administrators instead — register the account, confirm its e-mail,
-- then run set-admin-role.sql against DEV with that address. After the first, an Administrator adds
-- the rest from the admin console.
--
-- Run it by hand only against a local database, after prod-bootstrap.sql: it needs generate_ulid()
-- and the cleansia-cz tenant row that file creates.
--
-- WHY THE HASH IS A LITERAL. Password is stored as v2$ + base64(salt[16] ‖
-- PBKDF2-SHA256(password, salt, 600000, 32)) — see PasswordExtensions.HashAndSaltPassword. Postgres
-- cannot produce that here: pgcrypto is unavailable by design (Azure blocks it unless allow-listed),
-- and there is no core PBKDF2. SeededAdminCredentialsTests runs this exact literal through the real
-- VerifyPassword, so a change to the hashing parameters fails a test instead of silently locking the
-- local admin out.
--
-- Profile 100 = Administrator, AuthenticationType 1 = Internal (email + password, not Google/Apple).
-- AdminRole 1 = Administrator: the role that holds everything, including role assignment itself —
-- CK_Users_AdminRole_Profile refuses an administrator row without one (ADR-0066 D1).
--
-- THE GUARD IS "NO USERS AT ALL", not "this email is free": any database with a single real account
-- in it skips this insert instead of gaining an account whose password is published. Running it twice
-- is a no-op.
INSERT INTO public."Users" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "Email", "Password", "FirstName", "LastName",
  "Profile", "AdminRole", "AuthenticationType", "IsEmailConfirmed",
  "FailedLoginAttempts", "ConfirmationCodeAttempts", "ResetPasswordCodeAttempts",
  "MustChangePassword", "PreferredLanguageCode", "TenantId"
)
SELECT generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP,
       'admin@cleansia.local',
       'v2$qZh8Ie/E1KhtIu0D3H9/nYe8kBsd96nMjcmoUiAbE8to1ifT7R6D4ZQ2yoe6tCyW',
       'Dev', 'Administrator',
       100, 1, 1, true,
       0, 0, 0,
       false, 'en', 'cleansia-cz'
WHERE NOT EXISTS (SELECT 1 FROM public."Users");
