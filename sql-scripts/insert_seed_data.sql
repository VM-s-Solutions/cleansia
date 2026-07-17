BEGIN TRANSACTION;

-- Temporarily defer foreign key constraint checks (Azure-compatible)
SET CONSTRAINTS ALL DEFERRED;

-- 1. FUNCTIONS
-- (No pgcrypto: Azure Postgres blocks it unless allow-listed. generate_ulid() below uses
--  core md5(random()) for its random bytes instead of pgcrypto's gen_random_bytes.)

CREATE 
OR REPLACE FUNCTION generate_ulid() RETURNS TEXT AS $inner$ DECLARE base32_chars TEXT := '0123456789ABCDEFGHJKMNPQRSTVWXYZ';
timestamp BIGINT;
random_bytes BYTEA;
ulid TEXT := '';
i INTEGER;
value BIGINT;
BEGIN timestamp := EXTRACT(
  EPOCH 
  FROM 
    CURRENT_TIMESTAMP
) * 1000;
IF timestamp > 281474976710655 THEN RAISE EXCEPTION 'Timestamp too large for ULID';
END IF;
random_bytes := SUBSTRING(DECODE(MD5(RANDOM()::TEXT || CLOCK_TIMESTAMP()::TEXT), 'hex') FROM 1 FOR 10);
value := timestamp;
FOR i IN 1..10 LOOP ulid := SUBSTRING(
  base32_chars 
  FROM 
    (value % 32 + 1):: INTEGER FOR 1
) || ulid;
value := value / 32;
END LOOP;
FOR i IN 0..9 LOOP value := GET_BYTE(random_bytes, i);
ulid := ulid || SUBSTRING(
  base32_chars 
  FROM 
    (value / 32 + 1):: INTEGER FOR 1
);
ulid := ulid || SUBSTRING(
  base32_chars 
  FROM 
    (value % 32 + 1):: INTEGER FOR 1
);
END LOOP;
IF LENGTH(ulid) > 26 THEN ulid := SUBSTRING(
  ulid 
  FROM 
    1 FOR 26
);
ELSIF LENGTH(ulid) < 26 THEN ulid := ulid || REPEAT(
  '0', 
  26 - LENGTH(ulid)
);
END IF;
RETURN ulid;
END;
$inner$ LANGUAGE plpgsql;

-- 2. LANGUAGES
INSERT INTO public."Languages" (
  "Id", "IsActive", "Code", "Name"
)
VALUES
  (generate_ulid():: TEXT, true, 'en', 'English'),
  (generate_ulid():: TEXT, true, 'cs', 'Čeština'),
  (generate_ulid():: TEXT, true, 'sk', 'Slovenčina'),
  (generate_ulid():: TEXT, true, 'uk', 'Українська'),
  (generate_ulid():: TEXT, true, 'ru', 'Русский');

-- 3. COUNTRIES
-- IsServiced flips the country into the customer/partner-facing pickers
-- (driven by Country/GetServiced). Seed only the country we actually
-- operate in today as serviced; admins can flip the flag on others via
-- the Service Area page when expanding.
INSERT INTO public."Countries" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Name", "IsoCode", "Translations", "IsServiced"
)
VALUES
  -- Europe
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Czech Republic', 'CZE', '{"en": {"Name": "Czech Republic", "Description": "Central European country"}, "cs": {"Name": "Česká republika", "Description": "Středoevropská země"}, "ru": {"Name": "Чешская Республика", "Description": "Центральноевропейская страна"}}', true),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Slovakia', 'SVK', '{"en": {"Name": "Slovakia", "Description": "Central European country"}, "cs": {"Name": "Slovensko", "Description": "Středoevropská země"}, "ru": {"Name": "Словакия", "Description": "Центральноевропейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Poland', 'POL', '{"en": {"Name": "Poland", "Description": "Central European country"}, "cs": {"Name": "Polsko", "Description": "Středoevropská země"}, "ru": {"Name": "Польша", "Description": "Центральноевропейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Germany', 'DEU', '{"en": {"Name": "Germany", "Description": "Central European country"}, "cs": {"Name": "Německo", "Description": "Středoevropská země"}, "ru": {"Name": "Германия", "Description": "Центральноевропейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Austria', 'AUT', '{"en": {"Name": "Austria", "Description": "Central European country"}, "cs": {"Name": "Rakousko", "Description": "Středoevropská země"}, "ru": {"Name": "Австрия", "Description": "Центральноевропейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Hungary', 'HUN', '{"en": {"Name": "Hungary", "Description": "Central European country"}, "cs": {"Name": "Maďarsko", "Description": "Středoevropská země"}, "ru": {"Name": "Венгрия", "Description": "Центральноевропейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Slovenia', 'SVN', '{"en": {"Name": "Slovenia", "Description": "Central European country"}, "cs": {"Name": "Slovinsko", "Description": "Středoevropská země"}, "ru": {"Name": "Словения", "Description": "Центральноевропейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Croatia', 'HRV', '{"en": {"Name": "Croatia", "Description": "Southeastern European country"}, "cs": {"Name": "Chorvatsko", "Description": "Jihovýchodní evropská země"}, "ru": {"Name": "Хорватия", "Description": "Юго-восточная европейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Italy', 'ITA', '{"en": {"Name": "Italy", "Description": "Southern European country"}, "cs": {"Name": "Itálie", "Description": "Jihoevropská země"}, "ru": {"Name": "Италия", "Description": "Южноевропейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'France', 'FRA', '{"en": {"Name": "France", "Description": "Western European country"}, "cs": {"Name": "Francie", "Description": "Západoevropská země"}, "ru": {"Name": "Франция", "Description": "Западноевропейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Spain', 'ESP', '{"en": {"Name": "Spain", "Description": "Southwestern European country"}, "cs": {"Name": "Španělsko", "Description": "Jihozápadní evropská země"}, "ru": {"Name": "Испания", "Description": "Юго-западная европейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Portugal', 'PRT', '{"en": {"Name": "Portugal", "Description": "Southwestern European country"}, "cs": {"Name": "Portugalsko", "Description": "Jihozápadní evropská země"}, "ru": {"Name": "Португалия", "Description": "Юго-западная европейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Netherlands', 'NLD', '{"en": {"Name": "Netherlands", "Description": "Northwestern European country"}, "cs": {"Name": "Nizozemsko", "Description": "Severozápadní evropská země"}, "ru": {"Name": "Нидерланды", "Description": "Северо-западная европейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Belgium', 'BEL', '{"en": {"Name": "Belgium", "Description": "Western European country"}, "cs": {"Name": "Belgie", "Description": "Západoevropská země"}, "ru": {"Name": "Бельгия", "Description": "Западноевропейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Switzerland', 'CHE', '{"en": {"Name": "Switzerland", "Description": "Central European country"}, "cs": {"Name": "Švýcarsko", "Description": "Středoevropská země"}, "ru": {"Name": "Швейцария", "Description": "Центральноевропейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'United Kingdom', 'GBR', '{"en": {"Name": "United Kingdom", "Description": "Northwestern European country"}, "cs": {"Name": "Velká Británie", "Description": "Severozápadní evropská země"}, "ru": {"Name": "Великобритания", "Description": "Северо-западная европейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Ireland', 'IRL', '{"en": {"Name": "Ireland", "Description": "Northwestern European country"}, "cs": {"Name": "Irsko", "Description": "Severozápadní evropská země"}, "ru": {"Name": "Ирландия", "Description": "Северо-западная европейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Denmark', 'DNK', '{"en": {"Name": "Denmark", "Description": "Northern European country"}, "cs": {"Name": "Dánsko", "Description": "Severní evropská země"}, "ru": {"Name": "Дания", "Description": "Северная европейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Sweden', 'SWE', '{"en": {"Name": "Sweden", "Description": "Northern European country"}, "cs": {"Name": "Švédsko", "Description": "Severní evropská země"}, "ru": {"Name": "Швеция", "Description": "Северная европейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Norway', 'NOR', '{"en": {"Name": "Norway", "Description": "Northern European country"}, "cs": {"Name": "Norsko", "Description": "Severní evropská země"}, "ru": {"Name": "Норвегия", "Description": "Северная европейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Finland', 'FIN', '{"en": {"Name": "Finland", "Description": "Northern European country"}, "cs": {"Name": "Finsko", "Description": "Severní evropská země"}, "ru": {"Name": "Финляндия", "Description": "Северная европейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Estonia', 'EST', '{"en": {"Name": "Estonia", "Description": "Northern European country"}, "cs": {"Name": "Estonsko", "Description": "Severní evropská země"}, "ru": {"Name": "Эстония", "Description": "Северная европейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Latvia', 'LVA', '{"en": {"Name": "Latvia", "Description": "Northern European country"}, "cs": {"Name": "Lotyšsko", "Description": "Severní evropská země"}, "ru": {"Name": "Латвия", "Description": "Северная европейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Lithuania', 'LTU', '{"en": {"Name": "Lithuania", "Description": "Northern European country"}, "cs": {"Name": "Litva", "Description": "Severní evropská země"}, "ru": {"Name": "Литва", "Description": "Северная европейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Romania', 'ROU', '{"en": {"Name": "Romania", "Description": "Southeastern European country"}, "cs": {"Name": "Rumunsko", "Description": "Jihovýchodní evropská země"}, "ru": {"Name": "Румыния", "Description": "Юго-восточная европейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Bulgaria', 'BGR', '{"en": {"Name": "Bulgaria", "Description": "Southeastern European country"}, "cs": {"Name": "Bulharsko", "Description": "Jihovýchodní evropská země"}, "ru": {"Name": "Болгария", "Description": "Юго-восточная европейская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Greece', 'GRC', '{"en": {"Name": "Greece", "Description": "Southeastern European country"}, "cs": {"Name": "Řecko", "Description": "Jihovýchodní evropská země"}, "ru": {"Name": "Греция", "Description": "Юго-восточная европейская страна"}}', false),
  -- North America
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'United States', 'USA', '{"en": {"Name": "United States", "Description": "North American country"}, "cs": {"Name": "Spojené státy", "Description": "Severoamerická země"}, "ru": {"Name": "Соединенные Штаты", "Description": "Североамериканская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Canada', 'CAN', '{"en": {"Name": "Canada", "Description": "North American country"}, "cs": {"Name": "Kanada", "Description": "Severoamerická země"}, "ru": {"Name": "Канада", "Description": "Североамериканская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Mexico', 'MEX', '{"en": {"Name": "Mexico", "Description": "North American country"}, "cs": {"Name": "Mexiko", "Description": "Severoamerická země"}, "ru": {"Name": "Мексика", "Description": "Североамериканская страна"}}', false),
  -- Asia
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Russia', 'RUS', '{"en": {"Name": "Russia", "Description": "Eurasian country"}, "cs": {"Name": "Rusko", "Description": "Euroasijská země"}, "ru": {"Name": "Россия", "Description": "Евразийская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'China', 'CHN', '{"en": {"Name": "China", "Description": "East Asian country"}, "cs": {"Name": "Čína", "Description": "Východoasijská země"}, "ru": {"Name": "Китай", "Description": "Восточноазиатская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Japan', 'JPN', '{"en": {"Name": "Japan", "Description": "East Asian country"}, "cs": {"Name": "Japonsko", "Description": "Východoasijská země"}, "ru": {"Name": "Япония", "Description": "Восточноазиатская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'South Korea', 'KOR', '{"en": {"Name": "South Korea", "Description": "East Asian country"}, "cs": {"Name": "Jižní Korea", "Description": "Východoasijská země"}, "ru": {"Name": "Южная Корея", "Description": "Восточноазиатская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'India', 'IND', '{"en": {"Name": "India", "Description": "South Asian country"}, "cs": {"Name": "Indie", "Description": "Jihoasijská země"}, "ru": {"Name": "Индия", "Description": "Южноазиатская страна"}}', false),
  -- Oceania
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Australia', 'AUS', '{"en": {"Name": "Australia", "Description": "Oceanic country"}, "cs": {"Name": "Austrálie", "Description": "Oceánská země"}, "ru": {"Name": "Австралия", "Description": "Океанская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'New Zealand', 'NZL', '{"en": {"Name": "New Zealand", "Description": "Oceanic country"}, "cs": {"Name": "Nový Zéland", "Description": "Oceánská země"}, "ru": {"Name": "Новая Зеландия", "Description": "Океанская страна"}}', false),
  -- South America
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Brazil', 'BRA', '{"en": {"Name": "Brazil", "Description": "South American country"}, "cs": {"Name": "Brazílie", "Description": "Jihoamerická země"}, "ru": {"Name": "Бразилия", "Description": "Южноамериканская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Argentina', 'ARG', '{"en": {"Name": "Argentina", "Description": "South American country"}, "cs": {"Name": "Argentina", "Description": "Jihoamerická země"}, "ru": {"Name": "Аргентина", "Description": "Южноамериканская страна"}}', false),
  -- Africa
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'South Africa', 'ZAF', '{"en": {"Name": "South Africa", "Description": "Southern African country"}, "cs": {"Name": "Jižní Afrika", "Description": "Jihoafrická země"}, "ru": {"Name": "Южная Африка", "Description": "Южноафриканская страна"}}', false),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Egypt', 'EGY', '{"en": {"Name": "Egypt", "Description": "North African country"}, "cs": {"Name": "Egypt", "Description": "Severoafrická země"}, "ru": {"Name": "Египет", "Description": "Североафриканская страна"}}', false);

-- 3b. SERVICE CITIES
-- Cities the company actually serves within a serviced country. Customer
-- order creation must pick an address whose city matches one of these
-- (city-name match, case-insensitive). Employee addresses don't have to
-- match — cleaners can live anywhere and commute. See
-- planning/active/service-areas.md.
--
-- ZipPrefix is stored from v1 but NOT enforced by the v1 validator (city
-- name alone). Pre-populating it now means we don't need a backfill the
-- day enforcement turns on.
--
-- Seed list covers the 10 largest Czech cities (the only serviced country
-- today). Admins extend this via the admin Service Area page.
INSERT INTO public."ServiceCities" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
  "TenantId", "CountryId", "Name", "ZipPrefix"
)
SELECT generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
       NULL,
       (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1),
       city.name, city.zip_prefix
FROM (VALUES
  ('Praha',             '1'),
  ('Brno',              '6'),
  ('Ostrava',           '7'),
  ('Plzeň',             '3'),
  ('Liberec',           '46'),
  ('Olomouc',           '77'),
  ('České Budějovice',  '37'),
  ('Hradec Králové',    '50'),
  ('Ústí nad Labem',    '40'),
  ('Pardubice',         '53')
) AS city(name, zip_prefix);

-- 3c. SERVICE CITIES — Prague-region name variants.
-- The serviced check is an EXACT (case-insensitive) name match
-- (ServiceCityRepository.CityIsServicedAsync: Name.ToLower() == input), so
-- the single 'Praha' row above does NOT cover what real addresses carry:
--   • 'Prague' — the English exonym Mapbox geocoding returns in the en locale
--     (the seeded customer addresses use it too);
--   • 'Praha N' / 'Prague N' — the administrative-district forms geocoders and
--     users commonly produce ('Praha 5', 'Prague 2', …).
-- Without these variants a booking in Prague fails city.not_serviced purely
-- on the spelling of the city the address picker happened to emit.
--
-- ZipPrefix: districts 1-10 carry their natural postal prefix (Praha 1 = 110xx
-- … Praha 9 = 190xx, Praha 10 = 10xxx); districts 11-22 span mixed ranges of
-- the old postal districts, so they get the generic Prague '1' (unenforced in
-- v1 either way — see the note above).
--
-- Idempotent (NOT EXISTS per name), so this block can be re-run standalone
-- against a database that already holds the base list above.
INSERT INTO public."ServiceCities" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
  "TenantId", "CountryId", "Name", "ZipPrefix"
)
SELECT generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
       NULL,
       (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1),
       city.name, city.zip_prefix
FROM (VALUES
  -- English exonym
  ('Prague',     '1'),
  -- Czech district forms
  ('Praha 1',    '11'),
  ('Praha 2',    '12'),
  ('Praha 3',    '13'),
  ('Praha 4',    '14'),
  ('Praha 5',    '15'),
  ('Praha 6',    '16'),
  ('Praha 7',    '17'),
  ('Praha 8',    '18'),
  ('Praha 9',    '19'),
  ('Praha 10',   '10'),
  ('Praha 11',   '1'),
  ('Praha 12',   '1'),
  ('Praha 13',   '1'),
  ('Praha 14',   '1'),
  ('Praha 15',   '1'),
  ('Praha 16',   '1'),
  ('Praha 17',   '1'),
  ('Praha 18',   '1'),
  ('Praha 19',   '1'),
  ('Praha 20',   '1'),
  ('Praha 21',   '1'),
  ('Praha 22',   '1'),
  -- English district forms (mixed-locale geocoder output)
  ('Prague 1',   '11'),
  ('Prague 2',   '12'),
  ('Prague 3',   '13'),
  ('Prague 4',   '14'),
  ('Prague 5',   '15'),
  ('Prague 6',   '16'),
  ('Prague 7',   '17'),
  ('Prague 8',   '18'),
  ('Prague 9',   '19'),
  ('Prague 10',  '10'),
  ('Prague 11',  '1'),
  ('Prague 12',  '1'),
  ('Prague 13',  '1'),
  ('Prague 14',  '1'),
  ('Prague 15',  '1'),
  ('Prague 16',  '1'),
  ('Prague 17',  '1'),
  ('Prague 18',  '1'),
  ('Prague 19',  '1'),
  ('Prague 20',  '1'),
  ('Prague 21',  '1'),
  ('Prague 22',  '1')
) AS city(name, zip_prefix)
WHERE NOT EXISTS (
  SELECT 1
  FROM public."ServiceCities" sc
  WHERE sc."CountryId" = (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1)
    AND LOWER(sc."Name") = LOWER(city.name)
    AND sc."TenantId" IS NULL
);

-- 4. EMAIL TRANSLATIONS
INSERT INTO public."EmailTranslations" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Subject", "Title",
  "Header", "SubHeader", "GreetingWord",
  "Instruction", "CodeNote", "Footer",
  "EmailType", "LanguageId"
)
VALUES
  (
    generate_ulid():: TEXT,
    true,
    'admin@budex.ua',
    CURRENT_TIMESTAMP - INTERVAL '12 days',
    NULL,
    NULL,
    NULL,
    NULL,
    'Confirm your email for Cleansia',
    'Email Confirmation - Cleansia',
    'Cleansia',
    'Email Confirmation',
    'Welcome',
    'Thank you for registering. Please enter the code below on the confirmation page:',
    'The code is valid for 15 minutes. If you did not register, please ignore this email.',
    'Questions? Contact us:',
    1,
    (
      SELECT
        "Id"
      FROM
        public."Languages"
      WHERE
        "Code" = 'en'
    )
  ),
  (
    generate_ulid():: TEXT,
    true,
    'admin@budex.ua',
    CURRENT_TIMESTAMP - INTERVAL '12 days',
    NULL,
    NULL,
    NULL,
    NULL,
    'Potvrďte svůj e-mail pro Cleansia',
    'Potvrzení e-mailu - Cleansia',
    'Cleansia',
    'Potvrzení e-mailu',
    'Vítejte',
    'Děkujeme za registraci. Na stránce potvrzení zadejte níže uvedený kód:',
    'Kód je platný 15 minut. Pokud jste se neregistrovali, tuto e-mailovou zprávu ignorujte.',
    'Otázky? Kontaktujte nás:',
    1,
    (
      SELECT
        "Id"
      FROM
        public."Languages"
      WHERE
        "Code" = 'cs'
    )
  );

-- 5. CURRENCIES
INSERT INTO public."Currencies" (
  "Id", "IsActive", "IsDefault", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Code", "Symbol", "Name", "ExchangeRate"
)
VALUES
  -- European Currencies
  (generate_ulid()::TEXT, true, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'CZK', 'Kč', 'Czech Koruna', 1.0),
  (generate_ulid()::TEXT, true, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'EUR', '€', 'Euro', 0.041),
  (generate_ulid()::TEXT, true, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'USD', '$', 'US Dollar', 0.044),
  (generate_ulid()::TEXT, true, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'GBP', '£', 'British Pound', 0.035),
  (generate_ulid()::TEXT, true, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'PLN', 'zł', 'Polish Zloty', 0.18),
  (generate_ulid()::TEXT, true, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'CHF', 'CHF', 'Swiss Franc', 0.039),
  (generate_ulid()::TEXT, true, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'SEK', 'kr', 'Swedish Krona', 0.47),
  (generate_ulid()::TEXT, true, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'NOK', 'kr', 'Norwegian Krone', 0.47),
  (generate_ulid()::TEXT, true, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'DKK', 'kr', 'Danish Krone', 0.31),
  (generate_ulid()::TEXT, true, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'HUF', 'Ft', 'Hungarian Forint', 16.2),
  (generate_ulid()::TEXT, true, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'RON', 'lei', 'Romanian Leu', 0.20),
  (generate_ulid()::TEXT, true, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'BGN', 'лв', 'Bulgarian Lev', 0.080);

-- 6. SERVICE CATEGORIES
-- Slugs are the client-facing stable identifier (mobile maps them to icons/colors).
-- Keep slugs immutable once seeded; rename Name freely via admin.
INSERT INTO public."ServiceCategories" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Slug", "Name", "Description", "DisplayOrder", "Translations"
)
VALUES
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'home', 'Home', 'Everyday home cleaning services', 10,
   '{"en": {"Name": "Home", "Description": "Everyday home cleaning services"}, "cs": {"Name": "Domácnost", "Description": "Každodenní úklid domácnosti"}, "sk": {"Name": "Domácnosť", "Description": "Každodenné upratovanie domácnosti"}, "uk": {"Name": "Дім", "Description": "Щоденне прибирання будинку"}, "ru": {"Name": "Дом", "Description": "Ежедневная уборка дома"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'deep', 'Deep clean', 'Thorough and specialized cleaning', 20,
   '{"en": {"Name": "Deep clean", "Description": "Thorough and specialized cleaning"}, "cs": {"Name": "Hloubkový úklid", "Description": "Důkladné a specializované čištění"}, "sk": {"Name": "Hĺbkové čistenie", "Description": "Dôkladné a špecializované čistenie"}, "uk": {"Name": "Глибоке прибирання", "Description": "Ретельне та спеціалізоване прибирання"}, "ru": {"Name": "Глубокая уборка", "Description": "Тщательная и специализированная уборка"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'laundry', 'Laundry', 'Washing, ironing, and linen care', 30,
   '{"en": {"Name": "Laundry", "Description": "Washing, ironing, and linen care"}, "cs": {"Name": "Praní", "Description": "Praní, žehlení a péče o prádlo"}, "sk": {"Name": "Pranie", "Description": "Pranie, žehlenie a starostlivosť o bielizeň"}, "uk": {"Name": "Прання", "Description": "Прання, прасування та догляд за білизною"}, "ru": {"Name": "Стирка", "Description": "Стирка, глажка и уход за бельём"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'pet', 'Pet', 'Services tailored for pet owners', 40,
   '{"en": {"Name": "Pet", "Description": "Services tailored for pet owners"}, "cs": {"Name": "Mazlíčci", "Description": "Služby pro majitele domácích mazlíčků"}, "sk": {"Name": "Domáce zvieratá", "Description": "Služby pre majiteľov domácich miláčikov"}, "uk": {"Name": "Тварини", "Description": "Послуги для власників домашніх тварин"}, "ru": {"Name": "Питомцы", "Description": "Услуги для владельцев домашних животных"}}');

-- 7. SERVICES
INSERT INTO public."Services" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Name", "Description",
  "BasePrice", "PerRoomPrice", "EstimatedTime", "CategoryId", "Translations"
)
VALUES
  -- Basic Cleaning Services
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'General Cleaning', 'Standard cleaning of all rooms including dusting, vacuuming, and sanitizing',
   500.00, 150.00, 120,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'home'),
   '{"en": {"Name": "General Cleaning", "Description": "Standard cleaning of all rooms including dusting, vacuuming, and sanitizing"}, "cs": {"Name": "Obecný úklid", "Description": "Standardní úklid všech místností včetně otírání prachu, vysávání a dezinfekce"}, "ru": {"Name": "Общая уборка", "Description": "Стандартная уборка всех комнат включая протирание пыли, пылесос и дезинфекцию"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Deep Cleaning', 'Thorough cleaning including baseboards, inside appliances, and detailed sanitization',
   800.00, 250.00, 180,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'deep'),
   '{"en": {"Name": "Deep Cleaning", "Description": "Thorough cleaning including baseboards, inside appliances, and detailed sanitization"}, "cs": {"Name": "Hloubkový úklid", "Description": "Důkladný úklid včetně lišt, vnitřků spotřebičů a detailní dezinfekce"}, "ru": {"Name": "Глубокая уборка", "Description": "Тщательная уборка включая плинтуса, внутри бытовой техники и детальная дезинфекция"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Bathroom Cleaning', 'Specialized bathroom cleaning with tile scrubbing and grout cleaning',
   300.00, 0.00, 45,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'home'),
   '{"en": {"Name": "Bathroom Cleaning", "Description": "Specialized bathroom cleaning with tile scrubbing and grout cleaning"}, "cs": {"Name": "Úklid koupelny", "Description": "Specializovaný úklid koupelny s drhnáním dlaždic a čištěním spár"}, "ru": {"Name": "Уборка ванной", "Description": "Специализированная уборка ванной с чисткой плитки и швов"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Kitchen Deep Clean', 'Comprehensive kitchen cleaning including oven, refrigerator, and cabinets',
   400.00, 0.00, 90,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'deep'),
   '{"en": {"Name": "Kitchen Deep Clean", "Description": "Comprehensive kitchen cleaning including oven, refrigerator, and cabinets"}, "cs": {"Name": "Hloubkový úklid kuchyně", "Description": "Komplexní úklid kuchyně včetně trouby, lednice a skříněk"}, "ru": {"Name": "Глубокая уборка кухни", "Description": "Комплексная уборка кухни включая духовку, холодильник и шкафы"}}'),

  -- Specialized Services
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Window Cleaning', 'Interior and exterior window cleaning with streak-free finish',
   200.00, 50.00, 60,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'home'),
   '{"en": {"Name": "Window Cleaning", "Description": "Interior and exterior window cleaning with streak-free finish"}, "cs": {"Name": "Mytí oken", "Description": "Mytí oken zevnitř i zvenčí bez šmouh"}, "ru": {"Name": "Мытье окон", "Description": "Мытье окон изнутри и снаружи без разводов"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Carpet Cleaning', 'Professional carpet steam cleaning and stain removal',
   350.00, 100.00, 90,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'home'),
   '{"en": {"Name": "Carpet Cleaning", "Description": "Professional carpet steam cleaning and stain removal"}, "cs": {"Name": "Čištění koberců", "Description": "Profesionální parní čištění koberců a odstraňování skvrn"}, "ru": {"Name": "Чистка ковров", "Description": "Профессиональная паровая чистка ковров и удаление пятен"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Upholstery Cleaning', 'Deep cleaning of sofas, chairs, and fabric furniture',
   450.00, 0.00, 75,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'home'),
   '{"en": {"Name": "Upholstery Cleaning", "Description": "Deep cleaning of sofas, chairs, and fabric furniture"}, "cs": {"Name": "Čištění čalounění", "Description": "Hloubkové čištění sedaček, židlí a látkového nábytku"}, "ru": {"Name": "Чистка обивки", "Description": "Глубокая чистка диванов, кресел и тканевой мебели"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Post-Construction Cleanup', 'Specialized cleaning after renovation or construction work',
   1200.00, 300.00, 240,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'deep'),
   '{"en": {"Name": "Post-Construction Cleanup", "Description": "Specialized cleaning after renovation or construction work"}, "cs": {"Name": "Úklid po rekonstrukci", "Description": "Specializovaný úklid po rekonstrukci nebo stavebních pracích"}, "ru": {"Name": "Уборка после ремонта", "Description": "Специализированная уборка после ремонта или строительных работ"}}'),

  -- Premium Services
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Move-in/Move-out Cleaning', 'Complete cleaning for moving in or out of property',
   1000.00, 200.00, 180,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'deep'),
   '{"en": {"Name": "Move-in/Move-out Cleaning", "Description": "Complete cleaning for moving in or out of property"}, "cs": {"Name": "Úklid při stěhování", "Description": "Kompletní úklid při nastěhování nebo vystěhování z nemovitosti"}, "ru": {"Name": "Уборка при переезде", "Description": "Полная уборка при въезде или выезде из недвижимости"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Eco-Friendly Cleaning', 'Green cleaning using only eco-friendly and non-toxic products',
   600.00, 180.00, 135,
   (SELECT "Id" FROM public."ServiceCategories" WHERE "Slug" = 'home'),
   '{"en": {"Name": "Eco-Friendly Cleaning", "Description": "Green cleaning using only eco-friendly and non-toxic products"}, "cs": {"Name": "Ekologický úklid", "Description": "Zelený úklid používající pouze ekologické a netoxické produkty"}, "ru": {"Name": "Экологическая уборка", "Description": "Зеленая уборка с использованием только экологически чистых и нетоксичных продуктов"}}');

-- 7b. EXTRAS — booking add-ons (inside-oven, inside-fridge, etc.)
-- Prices are placeholders per the spec (booking-extras-and-surcharge.md §1a);
-- PM should sanity-check before production seed. All 5 locales translated
-- in-line so the GetExtraOverview endpoint serves localized strings out of
-- the box.
INSERT INTO public."Extras" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Slug", "Name", "Description",
  "Price", "DisplayOrder", "Translations"
)
VALUES
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'inside-oven', 'Inside oven cleaning',
   'Degrease, scrub, wipe down — bring the oven back to factory clean.',
   200.00, 10,
   '{"en": {"Name": "Inside oven cleaning", "Description": "Degrease, scrub, wipe down — bring the oven back to factory clean."}, "cs": {"Name": "Čištění vnitřku trouby", "Description": "Odmaštění, vydrhnutí, otření — trouba bude jako nová."}, "sk": {"Name": "Čistenie vnútra rúry", "Description": "Odmastenie, vydrhnutie, utretie — rúra bude ako nová."}, "uk": {"Name": "Чистка духовки зсередини", "Description": "Знежирення, миття, протирання — духовка як нова."}, "ru": {"Name": "Чистка духовки изнутри", "Description": "Обезжиривание, оттирание, протирка — духовка как новая."}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'inside-fridge', 'Inside fridge cleaning',
   'Empty, clean, wipe down, reassemble. Assumes the fridge has been emptied beforehand.',
   150.00, 20,
   '{"en": {"Name": "Inside fridge cleaning", "Description": "Empty, clean, wipe down, reassemble. Assumes the fridge has been emptied beforehand."}, "cs": {"Name": "Čištění vnitřku ledničky", "Description": "Vyprázdnit, vyčistit, otřít, složit zpět. Předpokládá vyprázdněnou ledničku."}, "sk": {"Name": "Čistenie vnútra chladničky", "Description": "Vyprázdniť, vyčistiť, utrieť, zložiť. Predpokladá vyprázdnenú chladničku."}, "uk": {"Name": "Чистка холодильника зсередини", "Description": "Випорожнити, помити, протерти, зібрати назад. Холодильник має бути порожнім."}, "ru": {"Name": "Чистка холодильника изнутри", "Description": "Освободить, помыть, протереть, собрать. Холодильник должен быть опустошён."}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'interior-windows', 'Interior windows',
   'Streak-free clean on the inside of the windows (exterior is a separate service).',
   100.00, 30,
   '{"en": {"Name": "Interior windows", "Description": "Streak-free clean on the inside of the windows (exterior is a separate service)."}, "cs": {"Name": "Vnitřní okna", "Description": "Mytí oken zevnitř bez šmouh (vnější strana je samostatná služba)."}, "sk": {"Name": "Vnútorné okná", "Description": "Umytie okien zvnútra bez šmúh (vonkajšia strana je samostatná služba)."}, "uk": {"Name": "Внутрішні вікна", "Description": "Прозоре миття вікон зсередини (зовнішня сторона — окрема послуга)."}, "ru": {"Name": "Окна изнутри", "Description": "Прозрачное мытьё окон изнутри (внешняя сторона — отдельная услуга)."}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'laundry-ironing', 'Laundry & ironing',
   'Up to one hour of laundry and ironing as part of the visit.',
   250.00, 40,
   '{"en": {"Name": "Laundry & ironing", "Description": "Up to one hour of laundry and ironing as part of the visit."}, "cs": {"Name": "Praní a žehlení", "Description": "Až jedna hodina praní a žehlení v rámci úklidu."}, "sk": {"Name": "Pranie a žehlenie", "Description": "Až jedna hodina prania a žehlenia v rámci upratovania."}, "uk": {"Name": "Прання та прасування", "Description": "До однієї години прання та прасування під час візиту."}, "ru": {"Name": "Стирка и глажка", "Description": "До часа стирки и глажки во время уборки."}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'pet-hair-supplement', 'Pet hair deep-clean',
   'Extra effort on pet hair removal for homes with shedding pets.',
   150.00, 50,
   '{"en": {"Name": "Pet hair deep-clean", "Description": "Extra effort on pet hair removal for homes with shedding pets."}, "cs": {"Name": "Důkladné odstranění zvířecích chlupů", "Description": "Extra péče o odstranění chlupů v domech s línajícími mazlíčky."}, "sk": {"Name": "Dôkladné odstránenie srsti", "Description": "Extra starostlivosť o odstránenie srsti v domoch s línajúcimi zvieratami."}, "uk": {"Name": "Глибоке прибирання шерсті тварин", "Description": "Додаткові зусилля для прибирання шерсті в домах з тваринами."}, "ru": {"Name": "Глубокая уборка шерсти животных", "Description": "Дополнительные усилия по уборке шерсти в домах с линяющими питомцами."}}');

-- 8. PACKAGES
INSERT INTO public."Packages" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Name", "Description", "Price", "Translations"
)
VALUES
  -- Basic Packages
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Essential Clean', 'Perfect for regular maintenance cleaning of your home',
   799.00,
   '{"en": {"Name": "Essential Clean", "Description": "Perfect for regular maintenance cleaning of your home"}, "cs": {"Name": "Základní úklid", "Description": "Ideální pro pravidelný udržovací úklid vašeho domova"}, "ru": {"Name": "Основная уборка", "Description": "Идеально для регулярной поддерживающей уборки вашего дома"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Complete Home Clean', 'Comprehensive cleaning package for the entire home',
   1299.00,
   '{"en": {"Name": "Complete Home Clean", "Description": "Comprehensive cleaning package for the entire home"}, "cs": {"Name": "Kompletní úklid domova", "Description": "Komplexní úklidový balíček pro celý domov"}, "ru": {"Name": "Полная уборка дома", "Description": "Комплексный пакет уборки для всего дома"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Deep Clean Premium', 'Intensive deep cleaning for thoroughly clean spaces',
   1799.00,
   '{"en": {"Name": "Deep Clean Premium", "Description": "Intensive deep cleaning for thoroughly clean spaces"}, "cs": {"Name": "Prémiový hloubkový úklid", "Description": "Intenzivní hloubkový úklid pro dokonale čisté prostory"}, "ru": {"Name": "Премиум глубокая уборка", "Description": "Интенсивная глубокая уборка для идеально чистых помещений"}}'),

  -- Specialized Packages
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Kitchen & Bathroom Focus', 'Specialized package focusing on kitchen and bathroom deep cleaning',
   999.00,
   '{"en": {"Name": "Kitchen & Bathroom Focus", "Description": "Specialized package focusing on kitchen and bathroom deep cleaning"}, "cs": {"Name": "Zaměření na kuchyň a koupelnu", "Description": "Specializovaný balíček zaměřený na hloubkový úklid kuchyně a koupelny"}, "ru": {"Name": "Фокус на кухню и ванную", "Description": "Специализированный пакет с акцентом на глубокую уборку кухни и ванной"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Eco-Green Package', 'Complete eco-friendly cleaning using only green products',
   1499.00,
   '{"en": {"Name": "Eco-Green Package", "Description": "Complete eco-friendly cleaning using only green products"}, "cs": {"Name": "Eko-zelený balíček", "Description": "Kompletní ekologický úklid používající pouze zelené produkty"}, "ru": {"Name": "Эко-зеленый пакет", "Description": "Полная экологическая уборка с использованием только зеленых продуктов"}}'),

  -- Premium Packages
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Moving Day Special', 'Perfect for move-in or move-out situations',
   2299.00,
   '{"en": {"Name": "Moving Day Special", "Description": "Perfect for move-in or move-out situations"}, "cs": {"Name": "Speciál pro den stěhování", "Description": "Ideální pro situace nastěhování nebo vystěhování"}, "ru": {"Name": "Специальный пакет для переезда", "Description": "Идеально для ситуаций въезда или выезда"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Post-Renovation Clean', 'Specialized cleaning after construction or renovation work',
   2799.00,
   '{"en": {"Name": "Post-Renovation Clean", "Description": "Specialized cleaning after construction or renovation work"}, "cs": {"Name": "Úklid po rekonstrukci", "Description": "Specializovaný úklid po stavebních nebo rekonstrukčních pracích"}, "ru": {"Name": "Уборка po ремонта", "Description": "Специализированная уборка после строительных или ремонтных работ"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Luxury Full Service', 'Premium package with all services included',
   3499.00,
   '{"en": {"Name": "Luxury Full Service", "Description": "Premium package with all services included"}, "cs": {"Name": "Luxusní kompletní služba", "Description": "Prémiový balíček se všemi zahrnutými službami"}, "ru": {"Name": "Роскошный полный сервис", "Description": "Премиум пакет со всеми включенными услугами"}}');

-- 12. ORDERS AND RELATED DATA
-- First insert package services relationships
INSERT INTO public."PackageServices" (
  "Id", "IsActive", "PackageId", "ServiceId"
)
VALUES
  -- Essential Clean Package Services
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Essential Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'General Cleaning' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Essential Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Bathroom Cleaning' LIMIT 1)),

  -- Complete Home Clean Package Services
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Complete Home Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'General Cleaning' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Complete Home Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Bathroom Cleaning' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Complete Home Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Window Cleaning' LIMIT 1)),

  -- Deep Clean Premium Package Services
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Deep Clean Premium' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Deep Cleaning' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Deep Clean Premium' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Kitchen Deep Clean' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Deep Clean Premium' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Bathroom Cleaning' LIMIT 1)),

  -- Kitchen & Bathroom Focus Package Services
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Kitchen & Bathroom Focus' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Kitchen Deep Clean' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Kitchen & Bathroom Focus' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Bathroom Cleaning' LIMIT 1)),

  -- Eco-Green Package Services
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Eco-Green Package' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Eco-Friendly Cleaning' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Eco-Green Package' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'General Cleaning' LIMIT 1)),

  -- Moving Day Special Package Services
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Moving Day Special' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Move-in/Move-out Cleaning' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Moving Day Special' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Deep Cleaning' LIMIT 1)),

  -- Post-Renovation Clean Package Services
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Post-Renovation Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Post-Construction Cleanup' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Post-Renovation Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Deep Cleaning' LIMIT 1)),

  -- Luxury Full Service Package Services
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Luxury Full Service' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Deep Cleaning' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Luxury Full Service' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Window Cleaning' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Luxury Full Service' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Carpet Cleaning' LIMIT 1)),
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Luxury Full Service' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Upholstery Cleaning' LIMIT 1));

-- ============================================================
-- COUNTRY INVOICE CONFIGS
-- ============================================================
INSERT INTO public."CountryInvoiceConfigs" (
  "Id", "IsActive", "CountryId", "VatRequired", "VatRate",
  "DigitalSignatureRequired", "EInvoiceFormat",
  "AdditionalFieldsJson", "LegalDisclaimerTemplate"
)
VALUES
  -- Czech Republic - VAT 21%
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1),
   true, 0.21, false, 'PDF', NULL,
   'This invoice is issued in accordance with Czech law. Payment terms: 14 days from issue date.'),

  -- Germany - VAT 19%
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'DEU' LIMIT 1),
   true, 0.19, false, 'PDF',
   '{"TaxNumber": "required", "UStIdNr": "optional"}',
   'Rechnung gemäß deutschem Steuerrecht. Zahlungsbedingungen: 14 Tage ab Rechnungsdatum.'),

  -- Austria - VAT 20%
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'AUT' LIMIT 1),
   true, 0.20, false, 'PDF', NULL,
   'Rechnung gemäß österreichischem Steuerrecht. Zahlungsbedingungen: 14 Tage ab Rechnungsdatum.'),

  -- Poland - VAT 23%
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'POL' LIMIT 1),
   true, 0.23, false, 'PDF',
   '{"NIP": "required"}',
   'Faktura wystawiona zgodnie z polskim prawem podatkowym. Termin płatności: 14 dni od daty wystawienia.'),

  -- Slovakia - VAT 20%
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'SVK' LIMIT 1),
   true, 0.20, false, 'PDF', NULL,
   'Faktúra vystavená v súlade so slovenským právom. Splatnosť: 14 dní odo dňa vystavenia.'),

  -- United States - No VAT
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'USA' LIMIT 1),
   false, 0.00, false, 'PDF',
   '{"EIN": "optional", "StateTaxId": "optional"}',
   'Invoice issued in accordance with US law. Payment terms: 14 days from invoice date.'),

  -- United Kingdom - VAT 20%
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'GBR' LIMIT 1),
   true, 0.20, false, 'PDF',
   '{"VATNumber": "required"}',
   'Invoice issued in accordance with UK law. Payment terms: 14 days from invoice date.'),

  -- France - VAT 20%
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'FRA' LIMIT 1),
   true, 0.20, false, 'PDF',
   '{"SIRET": "required"}',
   'Facture émise conformément à la loi française. Conditions de paiement: 14 jours à compter de la date d''émission.'),

  -- Italy - VAT 22%
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'ITA' LIMIT 1),
   true, 0.22, false, 'PDF+XML',
   '{"CodiceFiscale": "required", "PartitaIVA": "required"}',
   'Fattura emessa in conformità alla legge italiana. Condizioni di pagamento: 14 giorni dalla data di emissione.'),

  -- Spain - VAT 21%
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'ESP' LIMIT 1),
   true, 0.21, false, 'PDF',
   '{"NIF": "required"}',
   'Factura emitida de acuerdo con la ley española. Condiciones de pago: 14 días desde la fecha de emisión.');

-- ============================================================
-- COUNTRY CONFIGURATIONS
-- ============================================================
INSERT INTO public."CountryConfigurations" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
  "CountryId", "DefaultCurrencyCode", "DefaultLanguageCode",
  "DateFormat", "TimeZoneId", "PhonePrefix",
  "StandardVatRate", "ReducedVatRate",
  "TaxIdLabel", "TaxIdFormat",
  "RegistrationNumberLabel", "RegistrationNumberFormat", "RegistrationNumberRequired",
  "VatNumberLabel", "VatNumberFormat", "VatNumberRequired",
  "DefaultPaymentGateway"
)
VALUES
  -- Czech Republic — IČO (company ID) mandatory, DIČ (VAT ID) optional
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1),
   'CZK', 'cs', 'dd.MM.yyyy', 'Europe/Prague', '+420',
   0.21, 0.15, 'IČO', '^\d{8}$',
   'IČO', '^\d{8}$', true,
   'DIČ', '^CZ\d{8,10}$', false,
   'Stripe'),

  -- Slovakia — IČO mandatory, IČ DPH (VAT) optional
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'SVK' LIMIT 1),
   'EUR', 'sk', 'dd.MM.yyyy', 'Europe/Bratislava', '+421',
   0.20, 0.10, 'IČO', '^\d{8}$',
   'IČO', '^\d{8}$', true,
   'IČ DPH', '^SK\d{10}$', false,
   'Stripe'),

  -- Poland — NIP mandatory, EU VAT optional
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'POL' LIMIT 1),
   'PLN', 'pl', 'dd.MM.yyyy', 'Europe/Warsaw', '+48',
   0.23, 0.08, 'NIP', '^\d{10}$',
   'NIP', '^\d{10}$', true,
   'VAT UE', '^PL\d{10}$', false,
   'Stripe'),

  -- Germany — Steuernummer mandatory, USt-IdNr optional
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'DEU' LIMIT 1),
   'EUR', 'de', 'dd.MM.yyyy', 'Europe/Berlin', '+49',
   0.19, 0.07, 'Steuernummer', '^\d{10,13}$',
   'Steuernummer', '^\d{10,13}$', true,
   'USt-IdNr', '^DE\d{9}$', false,
   'Stripe'),

  -- Austria — Firmenbuchnummer mandatory, UID (VAT) optional
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'AUT' LIMIT 1),
   'EUR', 'de', 'dd.MM.yyyy', 'Europe/Vienna', '+43',
   0.20, 0.10, 'UID-Nummer', '^ATU\d{8}$',
   'Firmenbuchnummer', '^[A-Z]?\d{1,6}[a-z]?$', true,
   'UID-Nummer', '^ATU\d{8}$', false,
   'Stripe'),

  -- United Kingdom — UTR mandatory, VAT number optional
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'GBR' LIMIT 1),
   'GBP', 'en', 'dd/MM/yyyy', 'Europe/London', '+44',
   0.20, 0.05, 'UTR', '^\d{10}$',
   'UTR', '^\d{10}$', true,
   'VAT Number', '^GB\d{9}$', false,
   'Stripe'),

  -- France — SIRET mandatory, TVA intracommunautaire optional
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'FRA' LIMIT 1),
   'EUR', 'fr', 'dd/MM/yyyy', 'Europe/Paris', '+33',
   0.20, 0.055, 'SIRET', '^\d{14}$',
   'SIRET', '^\d{14}$', true,
   'TVA', '^FR[A-Z0-9]{2}\d{9}$', false,
   'Stripe'),

  -- Italy — Codice Fiscale mandatory, Partita IVA optional
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'ITA' LIMIT 1),
   'EUR', 'it', 'dd/MM/yyyy', 'Europe/Rome', '+39',
   0.22, 0.10, 'Codice Fiscale', '^[A-Z]{6}\d{2}[A-Z]\d{2}[A-Z]\d{3}[A-Z]$',
   'Codice Fiscale', '^[A-Z]{6}\d{2}[A-Z]\d{2}[A-Z]\d{3}[A-Z]$', true,
   'Partita IVA', '^IT\d{11}$', false,
   'Stripe'),

  -- Spain — NIF mandatory, NIF-IVA optional
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'ESP' LIMIT 1),
   'EUR', 'es', 'dd/MM/yyyy', 'Europe/Madrid', '+34',
   0.21, 0.10, 'NIF', '^[A-Z]\d{7}[A-Z0-9]$',
   'NIF', '^[A-Z]\d{7}[A-Z0-9]$', true,
   'NIF-IVA', '^ES[A-Z0-9]\d{7}[A-Z0-9]$', false,
   'Stripe'),

  -- United States — EIN mandatory, no separate VAT
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'USA' LIMIT 1),
   'USD', 'en', 'MM/dd/yyyy', 'America/New_York', '+1',
   0.00, NULL, 'EIN', '^\d{2}-\d{7}$',
   'EIN', '^\d{2}-\d{7}$', true,
   NULL, NULL, false,
   'Stripe');

-- ============================================================
-- FEATURE FLAGS
-- ============================================================
INSERT INTO public."FeatureFlags" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
  "Name", "Description", "IsEnabled", "Scope", "ScopeValue"
)
VALUES
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'StripePayments', 'Enable Stripe payment processing', true, 'global', NULL),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'EcoFriendlyBadge', 'Show eco-friendly badge on qualifying services', true, 'global', NULL),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'EmployeeSelfService', 'Allow employees to manage their own profiles', true, 'global', NULL),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'AutoInvoiceGeneration', 'Automatically generate invoices when pay period closes', true, 'global', NULL),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'DisputeSystem', 'Enable customer dispute/complaint system', true, 'global', NULL),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'PushNotifications', 'Enable push notifications for mobile app', false, 'global', NULL);

-- ============================================================
-- COMPANY INFO
-- ============================================================
INSERT INTO public."CompanyInfo" (
    "Id", "LegalName", "TradingName", "Tagline",
    "RegistrationNumber", "VatNumber", "IsVatPayer",
    "Street", "City", "ZipCode", "CountryId",
    "Phone", "Email", "Website",
    "BankName", "BankAccountNumber", "Iban", "Swift",
    "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn"
)
VALUES (
    generate_ulid()::TEXT,
    'Cleansia s.r.o.',
    'CLEANSIA',
    'Professional Cleaning Services',
    '12345678',  -- IČO (Registration Number) - REPLACE WITH ACTUAL
    'CZ12345678',  -- DIČ (VAT Number) - REPLACE WITH ACTUAL
    false,       -- IsVatPayer — seed company is not a VAT payer ("Nejsme plátci DPH")
    'Václavské náměstí 1',
    'Prague',
    '11000',
    (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1),
    '+420 123 456 789',
    'info@cleansia.cz',
    'https://www.cleansia.cz',
    'Česká spořitelna',
    '123456789/0800',
    'CZ65 0800 0000 1234 5678 9012',
    'GIBACZPX',
    true,
    'system',
    CURRENT_TIMESTAMP,
    'system',
    CURRENT_TIMESTAMP
);

-- ============================================================
-- EMAIL TEMPLATE TRANSLATIONS
-- ============================================================
INSERT INTO public."EmailTemplateTranslations" (
    "Id", "IsActive", "EmailType", "Key", "Value", "LanguageId",
    "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn"
)
VALUES
-- Password Reset - English (EmailType = 2 = ResetPassword)
(generate_ulid()::TEXT, true, 2, 'Subject', 'Reset Your Password', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'Greeting', 'Hello', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'IntroText', 'You requested to reset your password. Click the button below to proceed:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'ButtonText', 'Reset Password', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'AlternativeText', 'Or use this verification code:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'ExpiryNotice', 'This link will expire in 24 hours.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'IgnoreText', 'If you did not request this, please ignore this email.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'SupportText', 'Need help? Contact us at', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'SupportEmail', 'support@cleansia.com', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'Closing', 'Best regards,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'TeamName', 'The Cleansia Team', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'FooterText', '© 2026 Cleansia. All rights reserved.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Password Reset - Czech (EmailType = 2 = ResetPassword)
(generate_ulid()::TEXT, true, 2, 'Subject', 'Obnovení hesla', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'Greeting', 'Dobrý den', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'IntroText', 'Požádali jste o obnovení hesla. Klikněte na tlačítko níže pro pokračování:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'ButtonText', 'Obnovit heslo', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'AlternativeText', 'Nebo použijte tento ověřovací kód:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'ExpiryNotice', 'Platnost tohoto odkazu vyprší za 24 hodin.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'IgnoreText', 'Pokud jste o toto nepožádali, ignorujte prosím tento e-mail.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'SupportText', 'Potřebujete pomoc? Kontaktujte nás na', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'SupportEmail', 'support@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'Closing', 'S pozdravem,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'TeamName', 'Tým Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'FooterText', '© 2026 Cleansia. Všechna práva vyhrazena.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Order Receipt - English (EmailType = 3 = OrderReceipt)
(generate_ulid()::TEXT, true, 3, 'Subject', 'Your Order Receipt', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'Greeting', 'Dear', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'ThankYouText', 'Thank you for your order! We are pleased to confirm your booking.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'OrderDetailsTitle', 'Order Details', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'OrderNumberLabel', 'Order Number:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'OrderDateLabel', 'Order Date:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'TotalAmountLabel', 'Total Amount:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'AttachmentText', 'Please find your detailed receipt attached to this email.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'TrackOrderText', 'You can track your order status at any time:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'ButtonText', 'View Order Status', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'QuestionsText', 'If you have any questions about your order, please don''t hesitate to contact us.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'SupportText', 'Contact us at', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'SupportEmail', 'support@cleansia.com', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'Closing', 'Best regards,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'TeamName', 'The Cleansia Team', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'FooterText', '© 2026 Cleansia. All rights reserved.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Order Receipt - Czech (EmailType = 3 = OrderReceipt)
(generate_ulid()::TEXT, true, 3, 'Subject', 'Potvrzení objednávky', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'Greeting', 'Vážený zákazníku', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'ThankYouText', 'Děkujeme za Vaši objednávku! S potěšením potvrzujeme Vaši rezervaci.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'OrderDetailsTitle', 'Detaily objednávky', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'OrderNumberLabel', 'Číslo objednávky:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'OrderDateLabel', 'Datum objednávky:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'TotalAmountLabel', 'Celková částka:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'AttachmentText', 'Detailní účtenku naleznete v příloze tohoto e-mailu.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'TrackOrderText', 'Stav objednávky můžete kdykoliv sledovat:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'ButtonText', 'Zobrazit stav objednávky', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'QuestionsText', 'Pokud máte jakékoliv dotazy ohledně Vaší objednávky, neváhejte nás kontaktovat.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'SupportText', 'Kontaktujte nás na', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'SupportEmail', 'support@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'Closing', 'S pozdravem,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'TeamName', 'Tým Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'FooterText', '© 2026 Cleansia. Všechna práva vyhrazena.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Email Confirmation - English (EmailType = 1 = ConfirmationEmail)
(generate_ulid()::TEXT, true, 1, 'Subject', 'Confirm Your Email Address', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'Greeting', 'Welcome', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'IntroText', 'Thank you for registering with Cleansia! To complete your registration, please verify your email address.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'InstructionsText', 'Use the verification code below to confirm your email:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'CodeLabel', 'Verification Code:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'ExpiryNotice', 'This code will expire in 24 hours.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SecurityText', 'For security reasons, do not share this code with anyone.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'IgnoreText', 'If you did not create an account, please ignore this email.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SupportText', 'Need help? Contact us at', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SupportEmail', 'support@cleansia.com', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'Closing', 'Best regards,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'TeamName', 'The Cleansia Team', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'FooterText', '© 2026 Cleansia. All rights reserved.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Email Confirmation - Czech (EmailType = 1 = ConfirmationEmail)
(generate_ulid()::TEXT, true, 1, 'Subject', 'Potvrďte Vaši e-mailovou adresu', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'Greeting', 'Vítejte', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'IntroText', 'Děkujeme za registraci v Cleansia! Pro dokončení registrace prosím ověřte Vaši e-mailovou adresu.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'InstructionsText', 'Použijte níže uvedený ověřovací kód pro potvrzení e-mailu:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'CodeLabel', 'Ověřovací kód:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'ExpiryNotice', 'Platnost tohoto kódu vyprší za 24 hodin.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SecurityText', 'Z bezpečnostních důvodů tento kód s nikým nesdílejte.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'IgnoreText', 'Pokud jste účet nevytvářeli, ignorujte prosím tento e-mail.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SupportText', 'Potřebujete pomoc? Kontaktujte nás na', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SupportEmail', 'support@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'Closing', 'S pozdravem,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'TeamName', 'Tým Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'FooterText', '© 2026 Cleansia. Všechna práva vyhrazena.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Pay Period Closed - English
(generate_ulid()::TEXT, true, 4, 'Subject', 'Pay Period Closed', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'Greeting', 'Hello', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'IntroText', 'We are writing to inform you that the pay period has been automatically closed by our system.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'StatusText', 'Period Closed', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'DetailsText', 'All work completed during this period has been recorded. Your invoice will be generated and processed according to our standard payroll schedule.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'PeriodDetailsTitle', 'Period Details', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'PeriodLabelText', 'Period', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'StartDateText', 'Start Date', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'EndDateText', 'End Date', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'ClosedAtText', 'Closed At', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStepsTitle', 'What Happens Next?', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStep1', 'Your invoice will be automatically generated within 24-48 hours', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStep2', 'You will receive a separate email with your invoice details', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStep3', 'Payment will be processed according to the agreed payment schedule', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'ClosingText', 'Thank you for your hard work during this period!', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'ContactText', 'If you have any questions or concerns about this period closure, please don''t hesitate to contact us.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'SupportText', 'Need help? Contact us at', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'SupportEmail', 'it@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'TeamSignature', 'Best regards', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'TeamName', 'The Cleansia Team', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'FooterText', '© 2026 Cleansia. All rights reserved.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Pay Period Closed - Czech
(generate_ulid()::TEXT, true, 4, 'Subject', 'Platební období uzavřeno', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'Greeting', 'Dobrý den', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'IntroText', 'Píšeme Vám, abychom Vás informovali, že platební období bylo automaticky uzavřeno naším systémem.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'StatusText', 'Období uzavřeno', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'DetailsText', 'Veškerá práce dokončená během tohoto období byla zaznamenána. Vaše faktura bude vygenerována a zpracována podle našeho standardního platebního harmonogramu.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'PeriodDetailsTitle', 'Detaily období', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'PeriodLabelText', 'Období', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'StartDateText', 'Datum začátku', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'EndDateText', 'Datum konce', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'ClosedAtText', 'Uzavřeno v', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStepsTitle', 'Co se stane dál?', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStep1', 'Vaše faktura bude automaticky vygenerována do 24-48 hodin', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStep2', 'Obdržíte samostatný email s detaily vaší faktury', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStep3', 'Platba bude zpracována podle dohodnutého platebního harmonogramu', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'ClosingText', 'Děkujeme za vaši tvrdou práci během tohoto období!', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'ContactText', 'Pokud máte jakékoliv otázky nebo obavy ohledně uzavření tohoto období, neváhejte nás kontaktovat.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'SupportText', 'Potřebujete pomoc? Kontaktujte nás na', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'SupportEmail', 'it@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'TeamSignature', 'S pozdravem', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'TeamName', 'Tým Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'FooterText', '© 2026 Cleansia. Všechna práva vyhrazena.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Period End Reminder - English
(generate_ulid()::TEXT, true, 5, 'Subject', 'Pay Period Ending Soon', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'Greeting', 'Hello', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'IntroText', 'This is a friendly reminder that the current pay period is ending soon. Please ensure all pending tasks are completed before the period closes.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'CountdownTitle', 'Time Remaining', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'DaysRemainingText', '{0} days remaining', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'PeriodDetailsTitle', 'Period Details', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'PeriodLabelText', 'Period', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'StartDateText', 'Start Date', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'EndDateText', 'End Date', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ReminderText', 'Please make sure to complete all your pending orders and submit any outstanding documentation before the period closes.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItemsTitle', 'Action Items', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItem1', 'Complete all assigned orders', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItem2', 'Submit time tracking and work documentation', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItem3', 'Review your completed work for accuracy', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ClosingText', 'Thank you for staying on top of your responsibilities!', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ContactText', 'If you have any questions or need assistance, please don''t hesitate to reach out.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'SupportText', 'Need help? Contact us at', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'SupportEmail', 'it@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'TeamSignature', 'Best regards', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'TeamName', 'The Cleansia Team', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'FooterText', '© 2026 Cleansia. All rights reserved.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Period End Reminder - Czech
(generate_ulid()::TEXT, true, 5, 'Subject', 'Platební období brzy končí', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'Greeting', 'Dobrý den', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'IntroText', 'Toto je přátelská připomínka, že současné platební období brzy končí. Ujistěte se prosím, že všechny nevyřízené úkoly jsou dokončeny před uzavřením období.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'CountdownTitle', 'Zbývající čas', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'DaysRemainingText', 'Zbývá {0} dní', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'PeriodDetailsTitle', 'Detaily období', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'PeriodLabelText', 'Období', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'StartDateText', 'Datum začátku', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'EndDateText', 'Datum konce', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ReminderText', 'Ujistěte se prosím, že dokončíte všechny vaše nevyřízené objednávky a odešlete veškerou nedokončenou dokumentaci před uzavřením období.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItemsTitle', 'Akční body', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItem1', 'Dokončete všechny přiřazené objednávky', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItem2', 'Odešlete sledování času a pracovní dokumentaci', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItem3', 'Zkontrolujte dokončenou práci z hlediska přesnosti', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ClosingText', 'Děkujeme, že plníte své povinnosti včas!', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ContactText', 'Pokud máte jakékoliv otázky nebo potřebujete pomoc, neváhejte se ozvat.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'SupportText', 'Potřebujete pomoc? Kontaktujte nás na', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'SupportEmail', 'it@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'TeamSignature', 'S pozdravem', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'TeamName', 'Tým Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'FooterText', '© 2026 Cleansia. Všechna práva vyhrazena.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Password Reset - Slovak (EmailType = 2 = ResetPassword)
(generate_ulid()::TEXT, true, 2, 'Subject', 'Obnovenie hesla', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'Greeting', 'Dobrý deň', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'IntroText', 'Požiadali ste o obnovenie hesla. Kliknite na tlačidlo nižšie pre pokračovanie:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'ButtonText', 'Obnoviť heslo', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'AlternativeText', 'Alebo použite tento overovací kód:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'ExpiryNotice', 'Platnosť tohto odkazu uplynie za 24 hodín.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'IgnoreText', 'Ak ste o toto nepožiadali, ignorujte prosím tento e-mail.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'SupportText', 'Potrebujete pomoc? Kontaktujte nás na', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'SupportEmail', 'support@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'Closing', 'S pozdravom,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'TeamName', 'Tím Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'FooterText', '© 2026 Cleansia. Všetky práva vyhradené.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Password Reset - Ukrainian (EmailType = 2 = ResetPassword)
(generate_ulid()::TEXT, true, 2, 'Subject', 'Скидання пароля', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'Greeting', 'Вітаємо', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'IntroText', 'Ви надіслали запит на скидання пароля. Натисніть кнопку нижче, щоб продовжити:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'ButtonText', 'Скинути пароль', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'AlternativeText', 'Або скористайтеся цим кодом підтвердження:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'ExpiryNotice', 'Термін дії цього посилання закінчується через 24 години.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'IgnoreText', 'Якщо ви не надсилали такого запиту, просто проігноруйте цей лист.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'SupportText', 'Потрібна допомога? Зв''яжіться з нами за адресою', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'SupportEmail', 'support@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'Closing', 'З повагою,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'TeamName', 'Команда Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'FooterText', '© 2026 Cleansia. Усі права захищені.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Password Reset - Russian (EmailType = 2 = ResetPassword)
(generate_ulid()::TEXT, true, 2, 'Subject', 'Сброс пароля', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'Greeting', 'Здравствуйте', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'IntroText', 'Вы запросили сброс пароля. Нажмите кнопку ниже, чтобы продолжить:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'ButtonText', 'Сбросить пароль', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'AlternativeText', 'Или используйте этот проверочный код:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'ExpiryNotice', 'Срок действия этой ссылки истекает через 24 часа.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'IgnoreText', 'Если вы не отправляли этот запрос, просто проигнорируйте это письмо.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'SupportText', 'Нужна помощь? Свяжитесь с нами по адресу', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'SupportEmail', 'support@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'Closing', 'С уважением,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'TeamName', 'Команда Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'FooterText', '© 2026 Cleansia. Все права защищены.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Order Receipt - Slovak (EmailType = 3 = OrderReceipt)
(generate_ulid()::TEXT, true, 3, 'Subject', 'Potvrdenie objednávky', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'Greeting', 'Vážený zákazník', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'ThankYouText', 'Ďakujeme za Vašu objednávku! S potešením potvrdzujeme Vašu rezerváciu.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'OrderDetailsTitle', 'Detaily objednávky', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'OrderNumberLabel', 'Číslo objednávky:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'OrderDateLabel', 'Dátum objednávky:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'TotalAmountLabel', 'Celková suma:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'AttachmentText', 'Detailnú účtenku nájdete v prílohe tohto e-mailu.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'TrackOrderText', 'Stav objednávky môžete kedykoľvek sledovať:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'ButtonText', 'Zobraziť stav objednávky', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'QuestionsText', 'Ak máte akékoľvek otázky ohľadom Vašej objednávky, neváhajte nás kontaktovať.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'SupportText', 'Kontaktujte nás na', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'SupportEmail', 'support@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'Closing', 'S pozdravom,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'TeamName', 'Tím Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'FooterText', '© 2026 Cleansia. Všetky práva vyhradené.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Order Receipt - Ukrainian (EmailType = 3 = OrderReceipt)
(generate_ulid()::TEXT, true, 3, 'Subject', 'Підтвердження замовлення', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'Greeting', 'Шановний клієнте', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'ThankYouText', 'Дякуємо за Ваше замовлення! Із задоволенням підтверджуємо Ваше бронювання.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'OrderDetailsTitle', 'Деталі замовлення', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'OrderNumberLabel', 'Номер замовлення:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'OrderDateLabel', 'Дата замовлення:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'TotalAmountLabel', 'Загальна сума:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'AttachmentText', 'Детальну квитанцію знайдете у вкладенні до цього листа.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'TrackOrderText', 'Ви можете відстежувати статус замовлення у будь-який час:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'ButtonText', 'Переглянути статус замовлення', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'QuestionsText', 'Якщо у Вас є питання щодо замовлення, не вагайтеся звернутися до нас.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'SupportText', 'Зв''яжіться з нами за адресою', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'SupportEmail', 'support@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'Closing', 'З повагою,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'TeamName', 'Команда Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'FooterText', '© 2026 Cleansia. Усі права захищені.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Order Receipt - Russian (EmailType = 3 = OrderReceipt)
(generate_ulid()::TEXT, true, 3, 'Subject', 'Подтверждение заказа', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'Greeting', 'Уважаемый клиент', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'ThankYouText', 'Благодарим за Ваш заказ! С удовольствием подтверждаем Ваше бронирование.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'OrderDetailsTitle', 'Детали заказа', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'OrderNumberLabel', 'Номер заказа:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'OrderDateLabel', 'Дата заказа:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'TotalAmountLabel', 'Общая сумма:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'AttachmentText', 'Подробную квитанцию Вы найдёте во вложении к этому письму.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'TrackOrderText', 'Вы можете отслеживать статус заказа в любое время:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'ButtonText', 'Посмотреть статус заказа', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'QuestionsText', 'Если у Вас есть вопросы по заказу, пожалуйста, свяжитесь с нами.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'SupportText', 'Свяжитесь с нами по адресу', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'SupportEmail', 'support@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'Closing', 'С уважением,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'TeamName', 'Команда Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'FooterText', '© 2026 Cleansia. Все права защищены.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Email Confirmation - Slovak (EmailType = 1 = ConfirmationEmail)
(generate_ulid()::TEXT, true, 1, 'Subject', 'Potvrďte Vašu e-mailovú adresu', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'Greeting', 'Vitajte', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'IntroText', 'Ďakujeme za registráciu v Cleansia! Pre dokončenie registrácie prosím overte Vašu e-mailovú adresu.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'InstructionsText', 'Použite nižšie uvedený overovací kód na potvrdenie e-mailu:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'CodeLabel', 'Overovací kód:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'ExpiryNotice', 'Platnosť tohto kódu uplynie za 24 hodín.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SecurityText', 'Z bezpečnostných dôvodov tento kód s nikým nezdieľajte.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'IgnoreText', 'Ak ste si účet nevytvárali, ignorujte prosím tento e-mail.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SupportText', 'Potrebujete pomoc? Kontaktujte nás na', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SupportEmail', 'support@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'Closing', 'S pozdravom,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'TeamName', 'Tím Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'FooterText', '© 2026 Cleansia. Všetky práva vyhradené.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Email Confirmation - Ukrainian (EmailType = 1 = ConfirmationEmail)
(generate_ulid()::TEXT, true, 1, 'Subject', 'Підтвердіть свою електронну адресу', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'Greeting', 'Вітаємо', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'IntroText', 'Дякуємо за реєстрацію в Cleansia! Щоб завершити реєстрацію, підтвердіть свою електронну адресу.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'InstructionsText', 'Використайте наведений нижче код підтвердження для верифікації електронної пошти:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'CodeLabel', 'Код підтвердження:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'ExpiryNotice', 'Термін дії цього коду закінчується через 24 години.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SecurityText', 'З міркувань безпеки не діліться цим кодом ні з ким.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'IgnoreText', 'Якщо Ви не створювали обліковий запис, просто проігноруйте цей лист.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SupportText', 'Потрібна допомога? Зв''яжіться з нами за адресою', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SupportEmail', 'support@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'Closing', 'З повагою,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'TeamName', 'Команда Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'FooterText', '© 2026 Cleansia. Усі права захищені.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Email Confirmation - Russian (EmailType = 1 = ConfirmationEmail)
(generate_ulid()::TEXT, true, 1, 'Subject', 'Подтвердите ваш адрес электронной почты', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'Greeting', 'Добро пожаловать', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'IntroText', 'Благодарим за регистрацию в Cleansia! Чтобы завершить регистрацию, пожалуйста, подтвердите ваш адрес электронной почты.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'InstructionsText', 'Используйте приведённый ниже проверочный код для подтверждения электронной почты:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'CodeLabel', 'Проверочный код:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'ExpiryNotice', 'Срок действия этого кода истекает через 24 часа.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SecurityText', 'В целях безопасности не передавайте этот код никому.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'IgnoreText', 'Если вы не создавали учётную запись, просто проигнорируйте это письмо.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SupportText', 'Нужна помощь? Свяжитесь с нами по адресу', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SupportEmail', 'support@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'Closing', 'С уважением,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'TeamName', 'Команда Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'FooterText', '© 2026 Cleansia. Все права защищены.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Pay Period Closed - Slovak (EmailType = 4)
(generate_ulid()::TEXT, true, 4, 'Subject', 'Platobné obdobie uzavreté', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'Greeting', 'Dobrý deň', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'IntroText', 'Píšeme Vám, aby sme Vás informovali, že platobné obdobie bolo automaticky uzavreté naším systémom.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'StatusText', 'Obdobie uzavreté', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'DetailsText', 'Všetka práca dokončená počas tohto obdobia bola zaznamenaná. Vaša faktúra bude vygenerovaná a spracovaná podľa nášho štandardného platobného harmonogramu.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'PeriodDetailsTitle', 'Detaily obdobia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'PeriodLabelText', 'Obdobie', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'StartDateText', 'Dátum začiatku', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'EndDateText', 'Dátum konca', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'ClosedAtText', 'Uzavreté o', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStepsTitle', 'Čo sa stane ďalej?', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStep1', 'Vaša faktúra bude automaticky vygenerovaná do 24-48 hodín', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStep2', 'Obdržíte samostatný e-mail s detailmi Vašej faktúry', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStep3', 'Platba bude spracovaná podľa dohodnutého platobného harmonogramu', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'ClosingText', 'Ďakujeme za Vašu tvrdú prácu počas tohto obdobia!', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'ContactText', 'Ak máte akékoľvek otázky alebo obavy ohľadom uzavretia tohto obdobia, neváhajte nás kontaktovať.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'SupportText', 'Potrebujete pomoc? Kontaktujte nás na', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'SupportEmail', 'it@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'TeamSignature', 'S pozdravom', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'TeamName', 'Tím Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'FooterText', '© 2026 Cleansia. Všetky práva vyhradené.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Pay Period Closed - Ukrainian (EmailType = 4)
(generate_ulid()::TEXT, true, 4, 'Subject', 'Платіжний період закрито', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'Greeting', 'Вітаємо', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'IntroText', 'Повідомляємо Вас, що платіжний період було автоматично закрито нашою системою.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'StatusText', 'Період закрито', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'DetailsText', 'Уся робота, виконана протягом цього періоду, зафіксована. Ваш рахунок буде сформовано та оброблено згідно зі стандартним графіком виплат.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'PeriodDetailsTitle', 'Деталі періоду', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'PeriodLabelText', 'Період', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'StartDateText', 'Дата початку', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'EndDateText', 'Дата завершення', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'ClosedAtText', 'Закрито о', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStepsTitle', 'Що буде далі?', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStep1', 'Ваш рахунок буде автоматично сформовано протягом 24-48 годин', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStep2', 'Ви отримаєте окремий лист із деталями Вашого рахунку', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStep3', 'Оплату буде оброблено згідно з узгодженим графіком виплат', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'ClosingText', 'Дякуємо за Вашу наполегливу роботу протягом цього періоду!', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'ContactText', 'Якщо у Вас є запитання чи сумніви щодо закриття цього періоду, будь ласка, звертайтеся до нас.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'SupportText', 'Потрібна допомога? Зв''яжіться з нами за адресою', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'SupportEmail', 'it@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'TeamSignature', 'З повагою', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'TeamName', 'Команда Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'FooterText', '© 2026 Cleansia. Усі права захищені.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Pay Period Closed - Russian (EmailType = 4)
(generate_ulid()::TEXT, true, 4, 'Subject', 'Платёжный период закрыт', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'Greeting', 'Здравствуйте', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'IntroText', 'Сообщаем вам, что платёжный период был автоматически закрыт нашей системой.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'StatusText', 'Период закрыт', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'DetailsText', 'Вся работа, выполненная в течение этого периода, была зафиксирована. Ваш счёт будет сформирован и обработан согласно стандартному графику выплат.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'PeriodDetailsTitle', 'Детали периода', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'PeriodLabelText', 'Период', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'StartDateText', 'Дата начала', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'EndDateText', 'Дата окончания', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'ClosedAtText', 'Закрыто в', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStepsTitle', 'Что произойдёт дальше?', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStep1', 'Ваш счёт будет автоматически сформирован в течение 24-48 часов', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStep2', 'Вы получите отдельное письмо с деталями вашего счёта', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'NextStep3', 'Оплата будет обработана согласно согласованному графику выплат', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'ClosingText', 'Благодарим вас за упорную работу в течение этого периода!', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'ContactText', 'Если у вас есть вопросы или сомнения относительно закрытия этого периода, пожалуйста, свяжитесь с нами.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'SupportText', 'Нужна помощь? Свяжитесь с нами по адресу', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'SupportEmail', 'it@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'TeamSignature', 'С уважением', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'TeamName', 'Команда Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 4, 'FooterText', '© 2026 Cleansia. Все права защищены.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Period End Reminder - Slovak (EmailType = 5)
(generate_ulid()::TEXT, true, 5, 'Subject', 'Platobné obdobie sa čoskoro končí', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'Greeting', 'Dobrý deň', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'IntroText', 'Toto je priateľská pripomienka, že aktuálne platobné obdobie sa čoskoro končí. Uistite sa prosím, že všetky nevybavené úlohy sú dokončené pred uzavretím obdobia.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'CountdownTitle', 'Zostávajúci čas', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'DaysRemainingText', 'Zostáva {0} dní', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'PeriodDetailsTitle', 'Detaily obdobia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'PeriodLabelText', 'Obdobie', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'StartDateText', 'Dátum začiatku', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'EndDateText', 'Dátum konca', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ReminderText', 'Uistite sa prosím, že dokončíte všetky Vaše nevybavené objednávky a odošlete akúkoľvek nedokončenú dokumentáciu pred uzavretím obdobia.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItemsTitle', 'Úlohy na splnenie', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItem1', 'Dokončite všetky pridelené objednávky', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItem2', 'Odošlite sledovanie času a pracovnú dokumentáciu', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItem3', 'Skontrolujte dokončenú prácu z hľadiska presnosti', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ClosingText', 'Ďakujeme, že plníte svoje povinnosti včas!', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ContactText', 'Ak máte akékoľvek otázky alebo potrebujete pomoc, neváhajte sa ozvať.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'SupportText', 'Potrebujete pomoc? Kontaktujte nás na', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'SupportEmail', 'it@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'TeamSignature', 'S pozdravom', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'TeamName', 'Tím Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'FooterText', '© 2026 Cleansia. Všetky práva vyhradené.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'sk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Period End Reminder - Ukrainian (EmailType = 5)
(generate_ulid()::TEXT, true, 5, 'Subject', 'Платіжний період незабаром завершується', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'Greeting', 'Вітаємо', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'IntroText', 'Це дружнє нагадування про те, що поточний платіжний період незабаром завершується. Будь ласка, переконайтеся, що всі незавершені завдання виконані до закриття періоду.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'CountdownTitle', 'Залишилось часу', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'DaysRemainingText', 'Залишилось {0} днів', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'PeriodDetailsTitle', 'Деталі періоду', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'PeriodLabelText', 'Період', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'StartDateText', 'Дата початку', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'EndDateText', 'Дата завершення', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ReminderText', 'Переконайтеся, будь ласка, що Ви завершили всі незавершені замовлення та подали всю необхідну документацію до закриття періоду.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItemsTitle', 'Завдання до виконання', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItem1', 'Завершіть усі призначені замовлення', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItem2', 'Подайте облік часу та робочу документацію', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItem3', 'Перевірте виконану роботу на точність', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ClosingText', 'Дякуємо за Вашу відповідальність!', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ContactText', 'Якщо у Вас є запитання або потрібна допомога, не вагайтеся звертатися.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'SupportText', 'Потрібна допомога? Зв''яжіться з нами за адресою', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'SupportEmail', 'it@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'TeamSignature', 'З повагою', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'TeamName', 'Команда Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'FooterText', '© 2026 Cleansia. Усі права захищені.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'uk'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Period End Reminder - Russian (EmailType = 5)
(generate_ulid()::TEXT, true, 5, 'Subject', 'Платёжный период скоро заканчивается', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'Greeting', 'Здравствуйте', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'IntroText', 'Это дружеское напоминание о том, что текущий платёжный период скоро заканчивается. Пожалуйста, убедитесь, что все незавершённые задачи выполнены до закрытия периода.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'CountdownTitle', 'Оставшееся время', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'DaysRemainingText', 'Осталось {0} дней', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'PeriodDetailsTitle', 'Детали периода', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'PeriodLabelText', 'Период', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'StartDateText', 'Дата начала', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'EndDateText', 'Дата окончания', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ReminderText', 'Пожалуйста, убедитесь, что вы выполнили все незавершённые заказы и подали всю необходимую документацию до закрытия периода.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItemsTitle', 'Задачи к выполнению', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItem1', 'Завершите все назначенные заказы', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItem2', 'Отправьте учёт времени и рабочую документацию', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ActionItem3', 'Проверьте выполненную работу на точность', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ClosingText', 'Благодарим вас за ответственное отношение к обязанностям!', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'ContactText', 'Если у вас есть вопросы или нужна помощь, пожалуйста, обращайтесь.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'SupportText', 'Нужна помощь? Свяжитесь с нами по адресу', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'SupportEmail', 'it@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'TeamSignature', 'С уважением', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'TeamName', 'Команда Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 5, 'FooterText', '© 2026 Cleansia. Все права защищены.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL);

-- ============================================================
-- DISPUTES
-- ============================================================
-- Dispute seed data has been moved to insert_disputes.sql
-- Run that script separately after this one to populate disputes

-- ============================================================
-- LOYALTY TIER CONFIGS (Phase A defaults)
-- ============================================================
-- Idempotent inserts: each row is keyed by Tier so re-runs are safe.
-- TenantId NULL = single-tenant default (matches existing seed entries).
-- DiscountPercent stored as a fraction in [0, 1] (e.g. 0.05 = 5%).
INSERT INTO public."LoyaltyTierConfigs" (
    "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
    "DeactivatedBy", "DeactivatedOn", "TenantId",
    "Tier", "LifetimePointsThreshold", "DiscountPercent",
    "MinimumOrderAmountForDiscount", "PerksJson"
)
SELECT '01LTYBRONZE000000000000000', true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, NULL,
    1, 0, 0.0000, NULL,
    '[{"icon":"badge","labelKey":"loyalty.perks.welcome_badge"}]'
WHERE NOT EXISTS (SELECT 1 FROM public."LoyaltyTierConfigs" WHERE "Tier" = 1 AND "TenantId" IS NULL);

INSERT INTO public."LoyaltyTierConfigs" (
    "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
    "DeactivatedBy", "DeactivatedOn", "TenantId",
    "Tier", "LifetimePointsThreshold", "DiscountPercent",
    "MinimumOrderAmountForDiscount", "PerksJson"
)
SELECT '01LTYSILVER000000000000000', true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, NULL,
    2, 500, 0.0500, 1000.00,
    '[{"icon":"badge","labelKey":"loyalty.perks.welcome_badge"},{"icon":"percent","labelKey":"loyalty.perks.discount_5_above_1000"}]'
WHERE NOT EXISTS (SELECT 1 FROM public."LoyaltyTierConfigs" WHERE "Tier" = 2 AND "TenantId" IS NULL);

INSERT INTO public."LoyaltyTierConfigs" (
    "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
    "DeactivatedBy", "DeactivatedOn", "TenantId",
    "Tier", "LifetimePointsThreshold", "DiscountPercent",
    "MinimumOrderAmountForDiscount", "PerksJson"
)
SELECT '01LTYGOLD0000000000000000A', true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, NULL,
    3, 2000, 0.1000, NULL,
    '[{"icon":"badge","labelKey":"loyalty.perks.welcome_badge"},{"icon":"percent","labelKey":"loyalty.perks.discount_10"},{"icon":"support","labelKey":"loyalty.perks.priority_support"}]'
WHERE NOT EXISTS (SELECT 1 FROM public."LoyaltyTierConfigs" WHERE "Tier" = 3 AND "TenantId" IS NULL);

INSERT INTO public."LoyaltyTierConfigs" (
    "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
    "DeactivatedBy", "DeactivatedOn", "TenantId",
    "Tier", "LifetimePointsThreshold", "DiscountPercent",
    "MinimumOrderAmountForDiscount", "PerksJson"
)
SELECT '01LTYPLATINUM0000000000000', true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, NULL,
    4, 5000, 0.1200, 1000.00,
    '[{"icon":"badge","labelKey":"loyalty.perks.welcome_badge"},{"icon":"percent","labelKey":"loyalty.perks.discount_12"},{"icon":"support","labelKey":"loyalty.perks.priority_support"},{"icon":"star","labelKey":"loyalty.perks.dedicated_pool"}]'
WHERE NOT EXISTS (SELECT 1 FROM public."LoyaltyTierConfigs" WHERE "Tier" = 4 AND "TenantId" IS NULL);

-- LOY-003 — keep the seed in sync with the live tier values. Idempotent
-- UPDATE so re-running the seed against an existing DB picks up the new
-- numbers without needing a separate migration script. Bronze stays 0%
-- but gains the floor (cosmetic — 0% × anything = 0, but keeps the
-- minimum_order semantics consistent across tiers).
UPDATE public."LoyaltyTierConfigs"
   SET "DiscountPercent" = 0.0500,
       "MinimumOrderAmountForDiscount" = 1000.00
 WHERE "Tier" = 2 AND "TenantId" IS NULL;

UPDATE public."LoyaltyTierConfigs"
   SET "DiscountPercent" = 0.1000,
       "MinimumOrderAmountForDiscount" = 1000.00
 WHERE "Tier" = 3 AND "TenantId" IS NULL;

UPDATE public."LoyaltyTierConfigs"
   SET "DiscountPercent" = 0.1200,
       "MinimumOrderAmountForDiscount" = 1000.00,
       "PerksJson" = '[{"icon":"badge","labelKey":"loyalty.perks.welcome_badge"},{"icon":"percent","labelKey":"loyalty.perks.discount_12"},{"icon":"support","labelKey":"loyalty.perks.priority_support"},{"icon":"star","labelKey":"loyalty.perks.dedicated_pool"}]'
 WHERE "Tier" = 4 AND "TenantId" IS NULL;

-- ============================================================
-- PROMO CODES (Phase B seed)
-- ============================================================
-- Idempotent inserts keyed on Code (which is unique per tenant).
-- Type: 1 = PercentDiscount (uses DiscountPercent), 2 = FixedDiscount (uses DiscountAmount + CurrencyId).
-- DiscountPercent stored as fraction in [0, 1] — backend renders as percentage.
-- All single-tenant defaults (TenantId NULL).

-- WELCOME15 — 15% off first booking, no minimum, single-use per user.
INSERT INTO public."PromoCodes" (
    "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
    "DeactivatedBy", "DeactivatedOn", "TenantId",
    "Code", "Type", "DiscountPercent", "DiscountAmount", "CurrencyId",
    "MinimumOrderAmount", "MaxRedemptionsPerUser", "GlobalMaxRedemptions",
    "CurrentRedemptionsCount", "ValidFrom", "ValidUntil", "Description"
)
SELECT '01PROMOWELCOME150000000000', true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, NULL,
    'WELCOME15', 1, 0.1500, NULL, NULL,
    NULL, 1, NULL,
    0, NULL, NULL, 'Welcome offer — 15% off the first booking. Single-use per user.'
WHERE NOT EXISTS (SELECT 1 FROM public."PromoCodes" WHERE "Code" = 'WELCOME15' AND "TenantId" IS NULL);

-- SPRING20 — 20% off bookings >= 1500 CZK, single-use per user.
INSERT INTO public."PromoCodes" (
    "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
    "DeactivatedBy", "DeactivatedOn", "TenantId",
    "Code", "Type", "DiscountPercent", "DiscountAmount", "CurrencyId",
    "MinimumOrderAmount", "MaxRedemptionsPerUser", "GlobalMaxRedemptions",
    "CurrentRedemptionsCount", "ValidFrom", "ValidUntil", "Description"
)
SELECT '01PROMOSPRING20000000000A', true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, NULL,
    'SPRING20', 1, 0.2000, NULL, NULL,
    1500.00, 1, NULL,
    0, NULL, NULL, 'Seasonal — 20% off bookings of 1500 CZK or more.'
WHERE NOT EXISTS (SELECT 1 FROM public."PromoCodes" WHERE "Code" = 'SPRING20' AND "TenantId" IS NULL);

-- LOYAL10 — 10% off bookings >= 800 CZK, repeatable up to 5 times per user.
-- Useful for return-customer marketing pushes.
INSERT INTO public."PromoCodes" (
    "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
    "DeactivatedBy", "DeactivatedOn", "TenantId",
    "Code", "Type", "DiscountPercent", "DiscountAmount", "CurrencyId",
    "MinimumOrderAmount", "MaxRedemptionsPerUser", "GlobalMaxRedemptions",
    "CurrentRedemptionsCount", "ValidFrom", "ValidUntil", "Description"
)
SELECT '01PROMOLOYAL100000000000B', true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, NULL,
    'LOYAL10', 1, 0.1000, NULL, NULL,
    800.00, 5, NULL,
    0, NULL, NULL, 'Return-customer offer — 10% off bookings of 800 CZK or more, up to 5 uses per user.'
WHERE NOT EXISTS (SELECT 1 FROM public."PromoCodes" WHERE "Code" = 'LOYAL10' AND "TenantId" IS NULL);

-- ─── Cleansia Plus membership plans ───
-- Two plans: monthly + yearly. Yearly is priced at ~15% discount per month.
-- StripePriceId values are placeholders — replace with the actual Price ids
-- from the Stripe dashboard before deploying. The monthly→yearly upgrade
-- path (SwapMembershipPlan command) reads BillingInterval to know which
-- plan is the "upgrade target".
-- TrialPeriodDays is 14 on both; Stripe only honors the trial on the user's
-- first subscription per customer id, so resubscribers don't get another
-- free 14 days.

-- PLUS_MONTHLY — 199 Kč/month
INSERT INTO public."MembershipPlans" (
    "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
    "DeactivatedBy", "DeactivatedOn", "TenantId",
    "Code", "Name", "MonthlyPriceCzk", "StripePriceId",
    "DiscountPercentage", "FreeCancellationWindowHours", "AllowsExpressUpgrade",
    "BillingInterval", "TrialPeriodDays"
)
SELECT '01PLUSMONTHLY00000000000A', true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, NULL,
    'PLUS_MONTHLY', 'Cleansia Plus (Monthly)', 199.00, 'price_1TSiJ83KjMqxM0RBVaiKAF6r',
    5.00, 4, true,
    1, 14
WHERE NOT EXISTS (SELECT 1 FROM public."MembershipPlans" WHERE "Code" = 'PLUS_MONTHLY' AND "TenantId" IS NULL);

-- PLUS_YEARLY — 2030 Kč/year (≈169 Kč/month, 15% off vs monthly).
INSERT INTO public."MembershipPlans" (
    "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
    "DeactivatedBy", "DeactivatedOn", "TenantId",
    "Code", "Name", "MonthlyPriceCzk", "StripePriceId",
    "DiscountPercentage", "FreeCancellationWindowHours", "AllowsExpressUpgrade",
    "BillingInterval", "TrialPeriodDays"
)
SELECT '01PLUSYEARLY000000000000A', true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, NULL,
    'PLUS_YEARLY', 'Cleansia Plus (Annual)', 2030.00, 'price_1TSiJ83KjMqxM0RBrfMWdjrF',
    5.00, 4, true,
    2, 14
WHERE NOT EXISTS (SELECT 1 FROM public."MembershipPlans" WHERE "Code" = 'PLUS_YEARLY' AND "TenantId" IS NULL);

-- Constraints are checked at COMMIT when using SET CONSTRAINTS ALL DEFERRED

COMMIT;
