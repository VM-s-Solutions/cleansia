-- ============================================================
-- Give the two seeded Cleansia Plus plans their 14-day free trial back (DEV)
-- ============================================================
-- Usage: Execute via GitHub Actions (execute-sql.yml), environment DEV
--   Script: fix-plus-trial-14-days.sql
--
-- Owner ruling 2026-09-30: Cleansia Plus has a 14-day free trial again, on the monthly and the yearly
-- plan, with every benefit from day one. A DEV database seeded before that ruling carries
-- TrialPeriodDays = 0 on both seeded plans, and insert_seed_data.sql never updates a plan that
-- already exists, so re-running the seed does not change them.
--
-- Touches only the two seeded rows, by their seed ids, and only while their trial is 0 — a trial an
-- administrator has since set by hand is left as it is. Production plans are typed into the admin
-- console and carry other ids, so this changes nothing there; it is written for DEV all the same.
-- Running it again is a no-op.
-- ============================================================

BEGIN;

SELECT "Code", "TrialPeriodDays" AS "TrialPeriodDaysBefore"
FROM "MembershipPlans"
WHERE "Id" IN ('01PLUSMONTHLY00000000000A', '01PLUSYEARLY000000000000A')
ORDER BY "Code";

UPDATE "MembershipPlans"
SET "TrialPeriodDays" = 14,
    "UpdatedOn" = NOW(),
    "UpdatedBy" = 'admin-script'
WHERE "Id" IN ('01PLUSMONTHLY00000000000A', '01PLUSYEARLY000000000000A')
  AND "TrialPeriodDays" = 0;

SELECT "Code", "TrialPeriodDays" AS "TrialPeriodDaysAfter"
FROM "MembershipPlans"
WHERE "Id" IN ('01PLUSMONTHLY00000000000A', '01PLUSYEARLY000000000000A')
ORDER BY "Code";

COMMIT;
