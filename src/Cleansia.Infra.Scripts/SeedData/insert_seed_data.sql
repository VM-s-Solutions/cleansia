BEGIN TRANSACTION;

-- 1. EXTENSION + FUNCTIONS (unchanged)

CREATE EXTENSION IF NOT EXISTS pgcrypto;

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
random_bytes := gen_random_bytes(10);
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
  (generate_ulid():: TEXT, true, 'ru', 'Русский'),
  (generate_ulid():: TEXT, true, 'cs', 'Čeština');

-- 3. COUNTRIES
INSERT INTO public."Countries" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Name", "IsoCode", "Translations"
)
VALUES
  -- Europe
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Czech Republic', 'CZE', '{"en": {"Name": "Czech Republic", "Description": "Central European country"}, "cs": {"Name": "Česká republika", "Description": "Středoevropská země"}, "ru": {"Name": "Чешская Республика", "Description": "Центральноевропейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Slovakia', 'SVK', '{"en": {"Name": "Slovakia", "Description": "Central European country"}, "cs": {"Name": "Slovensko", "Description": "Středoevropská země"}, "ru": {"Name": "Словакия", "Description": "Центральноевропейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Poland', 'POL', '{"en": {"Name": "Poland", "Description": "Central European country"}, "cs": {"Name": "Polsko", "Description": "Středoevropská země"}, "ru": {"Name": "Польша", "Description": "Центральноевропейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Germany', 'DEU', '{"en": {"Name": "Germany", "Description": "Central European country"}, "cs": {"Name": "Německo", "Description": "Středoevropská země"}, "ru": {"Name": "Германия", "Description": "Центральноевропейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Austria', 'AUT', '{"en": {"Name": "Austria", "Description": "Central European country"}, "cs": {"Name": "Rakousko", "Description": "Středoevropská země"}, "ru": {"Name": "Австрия", "Description": "Центральноевропейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Hungary', 'HUN', '{"en": {"Name": "Hungary", "Description": "Central European country"}, "cs": {"Name": "Maďarsko", "Description": "Středoevropská země"}, "ru": {"Name": "Венгрия", "Description": "Центральноевропейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Slovenia', 'SVN', '{"en": {"Name": "Slovenia", "Description": "Central European country"}, "cs": {"Name": "Slovinsko", "Description": "Středoevropská země"}, "ru": {"Name": "Словения", "Description": "Центральноевропейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Croatia', 'HRV', '{"en": {"Name": "Croatia", "Description": "Southeastern European country"}, "cs": {"Name": "Chorvatsko", "Description": "Jihovýchodní evropská země"}, "ru": {"Name": "Хорватия", "Description": "Юго-восточная европейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Italy', 'ITA', '{"en": {"Name": "Italy", "Description": "Southern European country"}, "cs": {"Name": "Itálie", "Description": "Jihoevropská země"}, "ru": {"Name": "Италия", "Description": "Южноевропейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'France', 'FRA', '{"en": {"Name": "France", "Description": "Western European country"}, "cs": {"Name": "Francie", "Description": "Západoevropská země"}, "ru": {"Name": "Франция", "Description": "Западноевропейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Spain', 'ESP', '{"en": {"Name": "Spain", "Description": "Southwestern European country"}, "cs": {"Name": "Španělsko", "Description": "Jihozápadní evropská země"}, "ru": {"Name": "Испания", "Description": "Юго-западная европейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Portugal', 'PRT', '{"en": {"Name": "Portugal", "Description": "Southwestern European country"}, "cs": {"Name": "Portugalsko", "Description": "Jihozápadní evropská země"}, "ru": {"Name": "Португалия", "Description": "Юго-западная европейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Netherlands', 'NLD', '{"en": {"Name": "Netherlands", "Description": "Northwestern European country"}, "cs": {"Name": "Nizozemsko", "Description": "Severozápadní evropská země"}, "ru": {"Name": "Нидерланды", "Description": "Северо-западная европейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Belgium', 'BEL', '{"en": {"Name": "Belgium", "Description": "Western European country"}, "cs": {"Name": "Belgie", "Description": "Západoevropská země"}, "ru": {"Name": "Бельгия", "Description": "Западноевропейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Switzerland', 'CHE', '{"en": {"Name": "Switzerland", "Description": "Central European country"}, "cs": {"Name": "Švýcarsko", "Description": "Středoevropská země"}, "ru": {"Name": "Швейцария", "Description": "Центральноевропейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'United Kingdom', 'GBR', '{"en": {"Name": "United Kingdom", "Description": "Northwestern European country"}, "cs": {"Name": "Velká Británie", "Description": "Severozápadní evropská země"}, "ru": {"Name": "Великобритания", "Description": "Северо-западная европейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Ireland', 'IRL', '{"en": {"Name": "Ireland", "Description": "Northwestern European country"}, "cs": {"Name": "Irsko", "Description": "Severozápadní evropská země"}, "ru": {"Name": "Ирландия", "Description": "Северо-западная европейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Denmark', 'DNK', '{"en": {"Name": "Denmark", "Description": "Northern European country"}, "cs": {"Name": "Dánsko", "Description": "Severní evropská země"}, "ru": {"Name": "Дания", "Description": "Северная европейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Sweden', 'SWE', '{"en": {"Name": "Sweden", "Description": "Northern European country"}, "cs": {"Name": "Švédsko", "Description": "Severní evropská země"}, "ru": {"Name": "Швеция", "Description": "Северная европейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Norway', 'NOR', '{"en": {"Name": "Norway", "Description": "Northern European country"}, "cs": {"Name": "Norsko", "Description": "Severní evropská země"}, "ru": {"Name": "Норвегия", "Description": "Северная европейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Finland', 'FIN', '{"en": {"Name": "Finland", "Description": "Northern European country"}, "cs": {"Name": "Finsko", "Description": "Severní evropská země"}, "ru": {"Name": "Финляндия", "Description": "Северная европейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Estonia', 'EST', '{"en": {"Name": "Estonia", "Description": "Northern European country"}, "cs": {"Name": "Estonsko", "Description": "Severní evropská země"}, "ru": {"Name": "Эстония", "Description": "Северная европейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Latvia', 'LVA', '{"en": {"Name": "Latvia", "Description": "Northern European country"}, "cs": {"Name": "Lotyšsko", "Description": "Severní evropská země"}, "ru": {"Name": "Латвия", "Description": "Северная европейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Lithuania', 'LTU', '{"en": {"Name": "Lithuania", "Description": "Northern European country"}, "cs": {"Name": "Litva", "Description": "Severní evropská země"}, "ru": {"Name": "Литва", "Description": "Северная европейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Romania', 'ROU', '{"en": {"Name": "Romania", "Description": "Southeastern European country"}, "cs": {"Name": "Rumunsko", "Description": "Jihovýchodní evropská země"}, "ru": {"Name": "Румыния", "Description": "Юго-восточная европейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Bulgaria', 'BGR', '{"en": {"Name": "Bulgaria", "Description": "Southeastern European country"}, "cs": {"Name": "Bulharsko", "Description": "Jihovýchodní evropská země"}, "ru": {"Name": "Болгария", "Description": "Юго-восточная европейская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Greece', 'GRC', '{"en": {"Name": "Greece", "Description": "Southeastern European country"}, "cs": {"Name": "Řecko", "Description": "Jihovýchodní evropská země"}, "ru": {"Name": "Греция", "Description": "Юго-восточная европейская страна"}}'),
  -- North America
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'United States', 'USA', '{"en": {"Name": "United States", "Description": "North American country"}, "cs": {"Name": "Spojené státy", "Description": "Severoamerická země"}, "ru": {"Name": "Соединенные Штаты", "Description": "Североамериканская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Canada', 'CAN', '{"en": {"Name": "Canada", "Description": "North American country"}, "cs": {"Name": "Kanada", "Description": "Severoamerická země"}, "ru": {"Name": "Канада", "Description": "Североамериканская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Mexico', 'MEX', '{"en": {"Name": "Mexico", "Description": "North American country"}, "cs": {"Name": "Mexiko", "Description": "Severoamerická země"}, "ru": {"Name": "Мексика", "Description": "Североамериканская страна"}}'),
  -- Asia
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Russia', 'RUS', '{"en": {"Name": "Russia", "Description": "Eurasian country"}, "cs": {"Name": "Rusko", "Description": "Euroasijská země"}, "ru": {"Name": "Россия", "Description": "Евразийская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'China', 'CHN', '{"en": {"Name": "China", "Description": "East Asian country"}, "cs": {"Name": "Čína", "Description": "Východoasijská země"}, "ru": {"Name": "Китай", "Description": "Восточноазиатская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Japan', 'JPN', '{"en": {"Name": "Japan", "Description": "East Asian country"}, "cs": {"Name": "Japonsko", "Description": "Východoasijská země"}, "ru": {"Name": "Япония", "Description": "Восточноазиатская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'South Korea', 'KOR', '{"en": {"Name": "South Korea", "Description": "East Asian country"}, "cs": {"Name": "Jižní Korea", "Description": "Východoasijská země"}, "ru": {"Name": "Южная Корея", "Description": "Восточноазиатская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'India', 'IND', '{"en": {"Name": "India", "Description": "South Asian country"}, "cs": {"Name": "Indie", "Description": "Jihoasijská země"}, "ru": {"Name": "Индия", "Description": "Южноазиатская страна"}}'),
  -- Oceania
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Australia', 'AUS', '{"en": {"Name": "Australia", "Description": "Oceanic country"}, "cs": {"Name": "Austrálie", "Description": "Oceánská země"}, "ru": {"Name": "Австралия", "Description": "Океанская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'New Zealand', 'NZL', '{"en": {"Name": "New Zealand", "Description": "Oceanic country"}, "cs": {"Name": "Nový Zéland", "Description": "Oceánská země"}, "ru": {"Name": "Новая Зеландия", "Description": "Океанская страна"}}'),
  -- South America
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Brazil', 'BRA', '{"en": {"Name": "Brazil", "Description": "South American country"}, "cs": {"Name": "Brazílie", "Description": "Jihoamerická země"}, "ru": {"Name": "Бразилия", "Description": "Южноамериканская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Argentina', 'ARG', '{"en": {"Name": "Argentina", "Description": "South American country"}, "cs": {"Name": "Argentina", "Description": "Jihoamerická země"}, "ru": {"Name": "Аргентина", "Description": "Южноамериканская страна"}}'),
  -- Africa
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'South Africa', 'ZAF', '{"en": {"Name": "South Africa", "Description": "Southern African country"}, "cs": {"Name": "Jižní Afrika", "Description": "Jihoafrická země"}, "ru": {"Name": "Южная Африка", "Description": "Южноафриканская страна"}}'),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'Egypt', 'EGY', '{"en": {"Name": "Egypt", "Description": "North African country"}, "cs": {"Name": "Egypt", "Description": "Severoafrická země"}, "ru": {"Name": "Египет", "Description": "Североафриканская страна"}}');

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
COMMIT;
