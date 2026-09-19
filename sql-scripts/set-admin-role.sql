-- ============================================================
-- Make a user an administrator, by email — a hand tool
-- ============================================================
-- Usage: Execute via GitHub Actions (execute-sql.yml)
--   Script: set-admin-role.sql
--
-- Before running, set the target email and the admin role below.
-- Profile values: 1 = Customer, 2 = Employee, 100 = Administrator
-- AdminRole values (ADR-0066): 1 = Administrator, 2 = Manager, 3 = Support, 4 = Accountant
--
-- Both columns are set together: CK_Users_AdminRole_Profile refuses an administrator row without a
-- role and a customer or cleaner row with one, so flipping Profile alone fails. The first
-- administrator of a company must be an Administrator (1) — the console's role assignment and the
-- last-Administrator guard both need one to exist. Later role changes belong to the console.
-- ============================================================

-- >>> SET THE TARGET USER EMAIL AND ROLE HERE <<<
\set target_email 'it@cleansia.cz'
\set target_role 1

-- Show current state
SELECT "Id", "Email", "FirstName", "LastName", "Profile", "AdminRole", "IsEmailConfirmed"
FROM "Users"
WHERE "Email" = :'target_email';

-- Verify user exists and update
UPDATE "Users"
SET "Profile" = 100,
    "AdminRole" = :target_role,
    "IsEmailConfirmed" = true,
    "UpdatedOn" = NOW(),
    "UpdatedBy" = 'admin-script'
WHERE "Email" = :'target_email';

-- Check if any rows were updated
\if :ROW_COUNT = 0
\echo 'ERROR: User not found. Register the user first via the Customer app, then run this script.'
\quit
\endif

-- Confirm the update
SELECT "Id", "Email", "FirstName", "LastName", "Profile", "AdminRole", "IsEmailConfirmed"
FROM "Users"
WHERE "Email" = :'target_email';
