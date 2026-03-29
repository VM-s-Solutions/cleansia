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

-- Verify user exists before updating
DO $$
DECLARE
    user_count INT;
    user_email TEXT := :'target_email';
BEGIN
    SELECT COUNT(*) INTO user_count FROM "Users" WHERE "Email" = user_email;

    IF user_count = 0 THEN
        RAISE EXCEPTION 'User with email "%" not found. Register the user first via the Customer app, then run this script.', user_email;
    END IF;

    -- Update profile to Administrator
    UPDATE "Users"
    SET "Profile" = 100,
        "IsEmailConfirmed" = true,
        "UpdatedOn" = NOW(),
        "UpdatedBy" = 'admin-script'
    WHERE "Email" = user_email;

    RAISE NOTICE 'User "%" has been upgraded to Administrator (Profile=100)', user_email;
END $$;

-- Confirm the update
SELECT "Id", "Email", "FirstName", "LastName", "Profile", "IsEmailConfirmed"
FROM "Users"
WHERE "Email" = :'target_email';
