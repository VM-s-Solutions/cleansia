-- ============================================================
-- Set Administrator role for a user by email
-- ============================================================
-- Usage: Execute via GitHub Actions (execute-sql.yml)
--   Script: set-admin-role.sql
--
-- Before running, set the target email below.
-- Profile values: 1 = Customer, 2 = Employee, 100 = Administrator
-- ============================================================

-- >>> SET THE TARGET USER EMAIL HERE <<<
\set target_email 'it@cleansia.cz'

-- Show current state
SELECT "Id", "Email", "FirstName", "LastName", "Profile", "IsEmailConfirmed"
FROM "Users"
WHERE "Email" = :'target_email';

-- Verify user exists and update
UPDATE "Users"
SET "Profile" = 100,
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
SELECT "Id", "Email", "FirstName", "LastName", "Profile", "IsEmailConfirmed"
FROM "Users"
WHERE "Email" = :'target_email';
