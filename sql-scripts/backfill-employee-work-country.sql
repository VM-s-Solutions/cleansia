-- backfill-employee-work-country.sql
--
-- One-off backfill for the new `Employees.WorkCountryId` column
-- added in the `AddEmployeeWorkCountry` EF migration. Run AFTER
-- the migration has been applied; safe to re-run (idempotent
-- thanks to the `WorkCountryId IS NULL` guard).
--
-- Scope: ONLY employees already in ContractStatus.Approved (= 4)
-- get backfilled — pending / rejected cleaners will receive their
-- WorkCountryId at approval time via the updated ApproveEmployee
-- flow. Without this filter we would silently mark unapproved
-- cleaners as scoped to a jurisdiction the admin never signed off
-- on, which is exactly the invariant ApproveEmployee is now
-- supposed to enforce.
--
-- Source: residency country on the employee's Address. That is the
-- closest available proxy for "country the cleaner is currently
-- working in" until admins re-approve and pick the work
-- jurisdiction explicitly. Cleaners whose address has no country
-- (or no address at all) are left null and will surface in the
-- "still null" sanity output below — those need manual triage.

UPDATE "Employees" e
SET "WorkCountryId" = a."CountryId"
FROM "Addresses" a
WHERE e."AddressId" = a."Id"
  AND e."ContractStatus" = 4         -- ContractStatus.Approved
  AND e."WorkCountryId" IS NULL
  AND a."CountryId" IS NOT NULL;

-- Sanity output: how many approved employees still have no
-- WorkCountryId. Anything > 0 is a manual-triage list — either
-- the employee has no address, or the address has no country.
SELECT
    (SELECT COUNT(*) FROM "Employees" WHERE "WorkCountryId" IS NOT NULL) AS "EmployeesWithWorkCountry",
    (
        SELECT COUNT(*)
        FROM "Employees"
        WHERE "ContractStatus" = 4
          AND "WorkCountryId" IS NULL
    ) AS "ApprovedEmployeesStillMissingWorkCountry";
