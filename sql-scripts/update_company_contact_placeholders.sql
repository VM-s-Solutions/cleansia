-- ============================================================
-- Give the seeded company record its current contact values (DEV)
-- ============================================================
-- Usage: Execute via GitHub Actions (execute-sql.yml), environment DEV
--   Script: update_company_contact_placeholders.sql
--
-- Owner decision 2026-10-03: the company record's e-mail is support@cleansia.cz, the one support
-- address customers and cleaners see. The legal texts, the cleaner contracts, the receipts and the
-- invoices print it through the company record. A DEV database seeded before that decision holds
-- info@cleansia.cz, and insert_seed_data.sql never updates a company row that already exists.
--
-- Touches only a row that still holds the old seeded value, so an address an administrator has since
-- typed in is left as it is. Production's company record is typed into the admin console, so this
-- changes nothing there; it is written for DEV all the same. Running it again is a no-op.
-- ============================================================

BEGIN;

SELECT "TenantId", "LegalName", "Email" AS "EmailBefore"
FROM public."CompanyInfo"
ORDER BY "TenantId", "LegalName";

UPDATE public."CompanyInfo"
SET "Email" = 'support@cleansia.cz',
    "UpdatedOn" = NOW(),
    "UpdatedBy" = 'admin-script'
WHERE "Email" = 'info@cleansia.cz';

SELECT "TenantId", "LegalName", "Email" AS "EmailAfter"
FROM public."CompanyInfo"
ORDER BY "TenantId", "LegalName";

COMMIT;
