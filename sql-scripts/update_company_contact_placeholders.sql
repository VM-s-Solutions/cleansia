-- ============================================================
-- Give the seeded company record its current contact values (DEV)
-- ============================================================
-- Usage: Execute via GitHub Actions (execute-sql.yml), environment DEV
--   Script: update_company_contact_placeholders.sql
--
-- Owner decisions 2026-10-03:
--   - the company record's e-mail is support@cleansia.cz, the one support address customers and
--     cleaners see;
--   - the seeded phone is the literal placeholder <company_phone_number> until the real number is
--     entered in admin, because +420 123 456 789 read like a real number in the legal texts and the
--     cleaner contracts.
-- The legal texts, the cleaner contracts, the receipts and the invoices print both through the company
-- record. A DEV database seeded before these decisions holds info@cleansia.cz and +420 123 456 789,
-- and insert_seed_data.sql never updates a company row that already exists.
--
-- Each value is moved only on a row that still holds the old seeded one, so an address or a number an
-- administrator has since typed in is left as it is. Production's company record is typed into the
-- admin console, so this changes nothing there; it is written for DEV all the same. Running it again
-- is a no-op.
-- ============================================================

BEGIN;

SELECT "TenantId", "LegalName", "Email" AS "EmailBefore", "Phone" AS "PhoneBefore"
FROM public."CompanyInfo"
ORDER BY "TenantId", "LegalName";

UPDATE public."CompanyInfo"
SET "Email" = 'support@cleansia.cz',
    "UpdatedOn" = NOW(),
    "UpdatedBy" = 'admin-script'
WHERE "Email" = 'info@cleansia.cz';

UPDATE public."CompanyInfo"
SET "Phone" = '<company_phone_number>',
    "UpdatedOn" = NOW(),
    "UpdatedBy" = 'admin-script'
WHERE "Phone" = '+420 123 456 789';

SELECT "TenantId", "LegalName", "Email" AS "EmailAfter", "Phone" AS "PhoneAfter"
FROM public."CompanyInfo"
ORDER BY "TenantId", "LegalName";

COMMIT;
