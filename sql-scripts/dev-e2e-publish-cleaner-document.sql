-- DEV ONLY. Publishes a new version of each cleaner document a Czech cleaner must hold (the framework contract,
-- the self-billing agreement and the data processing agreement: LegalDocument.CleanerAcceptedTypes), for the E2E
-- harness's TAKE-025 (owner ruling 2026-11-15). Each new version is a copy of the one the resolver serves a CZ
-- cleaner today (LegalDocumentRepository.GetInForceAsync: the CZ copy before the platform-wide one, the latest
-- effective date first), with the same market, the same texts, and today's date as its effective date and version.
--
-- !! IT GATES EVERY CLEANER ON DEV: a cleaner's acceptance covers one document id (UserConsent.Covers), so from
-- !! the moment this commits no DEV cleaner can take a job until they accept the new version again (TakeOrder,
-- !! ApproveEmployee). Run it ONLY as the very last step of an E2E run, in the payroll invocation that sets
-- !! E2E_TAKE025=1, after every take of the run, the mobile lanes' included (e2e/README.md, "DEV facts a case must
-- !! respect"). Never on a scratch run.
--
-- It publishes only when the dispatch names it: Execute SQL Script with confirm set to this file's name, which the
-- DEV job hands the script as the session setting cleansia.confirm (scripts/publish-cleaner-document.sh does both).
-- It refuses to run on a database that does not hold the published DEV administrator, which PRO never does.
-- Idempotent: a type that already has a version dated today is left alone. Nothing in the repository changes, so
-- nothing of this reaches PRO; the start-up seeder never deletes or edits a row it did not seed.
-- DevE2eScriptsTests runs it against a migrated, seeded Postgres in CI.
BEGIN;

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "Users" WHERE "Email" = 'admin@cleansia.local') THEN
    RAISE EXCEPTION 'dev-e2e-publish-cleaner-document.sql is DEV only: the published DEV administrator is missing';
  END IF;
  IF current_setting('cleansia.confirm', true) IS DISTINCT FROM 'dev-e2e-publish-cleaner-document.sql' THEN
    RAISE EXCEPTION 'dev-e2e-publish-cleaner-document.sql gates every DEV cleaner: dispatch it with confirm=dev-e2e-publish-cleaner-document.sql, as the last step of an E2E run';
  END IF;
END $$;

-- A ULID-shaped id: ten characters of the millisecond clock, sixteen random, in Crockford base32. Each string_agg
-- names its own generate_series column, so it aggregates there and not over the outer query.
CREATE FUNCTION pg_temp.e2e_ulid() RETURNS text LANGUAGE sql VOLATILE AS $f$
  SELECT (SELECT string_agg(substr(a, ((floor(extract(epoch FROM clock_timestamp()) * 1000)::bigint >> (5 * (9 - i))) & 31)::int + 1, 1), '' ORDER BY i)
          FROM generate_series(0, 9) AS i)
      || (SELECT string_agg(substr(a, floor(random() * 32)::int + 1, 1), '' ORDER BY r) FROM generate_series(1, 16) AS r)
  FROM (SELECT '0123456789ABCDEFGHJKMNPQRSTVWXYZ'::text AS a) AS alphabet
$f$;

CREATE TEMP TABLE published ON COMMIT DROP AS
WITH cz AS (SELECT "Id" FROM "Countries" WHERE "IsoCode" = 'CZE'),
today AS (SELECT (now() AT TIME ZONE 'UTC')::date AS d),
served AS (
  -- What a CZ cleaner is served per type today (Audience 1 = Employee; types 3, 4, 5).
  SELECT DISTINCT ON (doc."Type") doc.*
  FROM "LegalDocuments" doc, cz, today
  WHERE doc."Audience" = 1
    AND doc."Type" IN (3, 4, 5)
    AND doc."EffectiveFrom" <= today.d
    AND (doc."CountryId" IS NULL OR doc."CountryId" = cz."Id")
  ORDER BY doc."Type", (doc."CountryId" IS NOT NULL) DESC, doc."EffectiveFrom" DESC
)
SELECT served."Id" AS old_id, pg_temp.e2e_ulid() AS new_id, served."Type", served."CountryId", served."Version" AS old_version, today.d
FROM served, today
WHERE served."EffectiveFrom" < today.d;

INSERT INTO "LegalDocuments" ("Id", "Audience", "Type", "CountryId", "EffectiveFrom", "Version", "Notes", "IsActive")
SELECT new_id, 1, "Type", "CountryId", d, to_char(d, 'YYYY-MM-DD'),
       'DEV only: an E2E run (TAKE-025), a copy of version ' || old_version, TRUE
FROM published;

INSERT INTO "LegalDocumentTexts" ("Id", "LegalDocumentId", "Language", "Title", "ContentMarkdown", "ContentHash", "IsActive")
SELECT pg_temp.e2e_ulid(), p.new_id, t."Language", t."Title", t."ContentMarkdown", t."ContentHash", t."IsActive"
FROM published p
JOIN "LegalDocumentTexts" t ON t."LegalDocumentId" = p.old_id;

-- After it, each of the three types serves a CZ cleaner a version dated today, whether this run or an earlier one wrote it.
DO $$
BEGIN
  IF (SELECT count(DISTINCT doc."Type") FROM "LegalDocuments" doc
      WHERE doc."Audience" = 1 AND doc."Type" IN (3, 4, 5) AND doc."EffectiveFrom" = (now() AT TIME ZONE 'UTC')::date
        AND (doc."CountryId" IS NULL OR doc."CountryId" = (SELECT "Id" FROM "Countries" WHERE "IsoCode" = 'CZE'))) <> 3 THEN
    RAISE EXCEPTION 'expected a version dated today of each of the three cleaner documents; nothing was committed';
  END IF;
END $$;

SELECT p."Type" AS document_type, p.old_version, to_char(p.d, 'YYYY-MM-DD') AS new_version, p.new_id AS new_document_id,
       (SELECT count(*) FROM "LegalDocumentTexts" t WHERE t."LegalDocumentId" = p.new_id) AS texts
FROM published p
ORDER BY p."Type";

COMMIT;
