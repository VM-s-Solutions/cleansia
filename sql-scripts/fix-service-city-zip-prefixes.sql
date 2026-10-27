-- ============================================================
-- Give the seeded Czech service cities the ZIP prefixes the booking gate now enforces (DEV)
-- ============================================================
-- Usage: Execute via GitHub Actions (execute-sql.yml), environment DEV
--   Script: fix-service-city-zip-prefixes.sql
--
-- Owner ruling 2026-10-25 (decision 9, "Praha first"): a service city's ZipPrefix is enforced. A row
-- with one serves an address whose postcode is five digits starting with it and refuses any other
-- valid postcode, whatever the city text says; a row without one serves by name. Every Prague row
-- (Praha, Prague, Praha 1-22, Prague 1-22) carries '1', and every other seeded city carries none,
-- because the prefixes seeded before were whole postal regions (6 is Brno and Znojmo) and must not be
-- enforced. prod-bootstrap.sql seeds those values now, but it never updates a row that already exists,
-- so a DEV database seeded before the ruling keeps the old ones.
--
-- Touches only the 55 seeded Czech rows, by name, and sets each to the seed's value whatever it holds:
-- a prefix typed while the admin console said it was not enforced was never a choice to enforce it.
-- No row is deleted and no row is switched on or off. Rows an administrator added are left as they
-- are; the last query lists every Czech row that still carries a prefix, so one that now gates
-- bookings is visible. Running it again is a no-op.
-- ============================================================

BEGIN;

SELECT "Name", "ZipPrefix" AS "ZipPrefixBefore", "IsActive"
FROM "ServiceCities"
WHERE "CountryId" = (SELECT "Id" FROM "Countries" WHERE "IsoCode" = 'CZE' LIMIT 1)
ORDER BY "Name";

UPDATE "ServiceCities" sc
SET "ZipPrefix" = seeded.zip_prefix,
    "UpdatedOn" = NOW(),
    "UpdatedBy" = 'admin-script'
FROM (VALUES
  ('Praha', '1'), ('Prague', '1'),
  ('Praha 1', '1'), ('Praha 2', '1'), ('Praha 3', '1'), ('Praha 4', '1'), ('Praha 5', '1'),
  ('Praha 6', '1'), ('Praha 7', '1'), ('Praha 8', '1'), ('Praha 9', '1'), ('Praha 10', '1'),
  ('Praha 11', '1'), ('Praha 12', '1'), ('Praha 13', '1'), ('Praha 14', '1'), ('Praha 15', '1'),
  ('Praha 16', '1'), ('Praha 17', '1'), ('Praha 18', '1'), ('Praha 19', '1'), ('Praha 20', '1'),
  ('Praha 21', '1'), ('Praha 22', '1'),
  ('Prague 1', '1'), ('Prague 2', '1'), ('Prague 3', '1'), ('Prague 4', '1'), ('Prague 5', '1'),
  ('Prague 6', '1'), ('Prague 7', '1'), ('Prague 8', '1'), ('Prague 9', '1'), ('Prague 10', '1'),
  ('Prague 11', '1'), ('Prague 12', '1'), ('Prague 13', '1'), ('Prague 14', '1'), ('Prague 15', '1'),
  ('Prague 16', '1'), ('Prague 17', '1'), ('Prague 18', '1'), ('Prague 19', '1'), ('Prague 20', '1'),
  ('Prague 21', '1'), ('Prague 22', '1'),
  ('Brno', NULL), ('Ostrava', NULL), ('Plzeň', NULL), ('Liberec', NULL), ('Olomouc', NULL),
  ('České Budějovice', NULL), ('Hradec Králové', NULL), ('Ústí nad Labem', NULL), ('Pardubice', NULL)
) AS seeded(name, zip_prefix)
WHERE sc."CountryId" = (SELECT "Id" FROM "Countries" WHERE "IsoCode" = 'CZE' LIMIT 1)
  AND LOWER(sc."Name") = LOWER(seeded.name)
  AND sc."ZipPrefix" IS DISTINCT FROM seeded.zip_prefix;

SELECT "Name", "ZipPrefix" AS "ZipPrefixAfter", "IsActive"
FROM "ServiceCities"
WHERE "CountryId" = (SELECT "Id" FROM "Countries" WHERE "IsoCode" = 'CZE' LIMIT 1)
  AND "ZipPrefix" IS NOT NULL
ORDER BY "Name";

COMMIT;
