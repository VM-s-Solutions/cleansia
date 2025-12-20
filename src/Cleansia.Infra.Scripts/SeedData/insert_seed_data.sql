BEGIN TRANSACTION;

-- Temporarily disable foreign key constraints to handle circular dependencies
SET session_replication_role = replica;

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

-- 6. SERVICES
INSERT INTO public."Services" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Name", "Description",
  "BasePrice", "PerRoomPrice", "EstimatedTime", "Translations"
)
VALUES
  -- Basic Cleaning Services
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'General Cleaning', 'Standard cleaning of all rooms including dusting, vacuuming, and sanitizing',
   500.00, 150.00, 120,
   '{"en": {"Name": "General Cleaning", "Description": "Standard cleaning of all rooms including dusting, vacuuming, and sanitizing"}, "cs": {"Name": "Obecný úklid", "Description": "Standardní úklid všech místností včetně otírání prachu, vysávání a dezinfekce"}, "ru": {"Name": "Общая уборка", "Description": "Стандартная уборка всех комнат включая протирание пыли, пылесос и дезинфекцию"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Deep Cleaning', 'Thorough cleaning including baseboards, inside appliances, and detailed sanitization',
   800.00, 250.00, 180,
   '{"en": {"Name": "Deep Cleaning", "Description": "Thorough cleaning including baseboards, inside appliances, and detailed sanitization"}, "cs": {"Name": "Hloubkový úklid", "Description": "Důkladný úklid včetně lišt, vnitřků spotřebičů a detailní dezinfekce"}, "ru": {"Name": "Глубокая уборка", "Description": "Тщательная уборка включая плинтуса, внутри бытовой техники и детальная дезинфекция"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Bathroom Cleaning', 'Specialized bathroom cleaning with tile scrubbing and grout cleaning',
   300.00, 0.00, 45,
   '{"en": {"Name": "Bathroom Cleaning", "Description": "Specialized bathroom cleaning with tile scrubbing and grout cleaning"}, "cs": {"Name": "Úklid koupelny", "Description": "Specializovaný úklid koupelny s drhnáním dlaždic a čištěním spár"}, "ru": {"Name": "Уборка ванной", "Description": "Специализированная уборка ванной с чисткой плитки и швов"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Kitchen Deep Clean', 'Comprehensive kitchen cleaning including oven, refrigerator, and cabinets',
   400.00, 0.00, 90,
   '{"en": {"Name": "Kitchen Deep Clean", "Description": "Comprehensive kitchen cleaning including oven, refrigerator, and cabinets"}, "cs": {"Name": "Hloubkový úklid kuchyně", "Description": "Komplexní úklid kuchyně včetně trouby, lednice a skříněk"}, "ru": {"Name": "Глубокая уборка кухни", "Description": "Комплексная уборка кухни включая духовку, холодильник и шкафы"}}'),

  -- Specialized Services
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Window Cleaning', 'Interior and exterior window cleaning with streak-free finish',
   200.00, 50.00, 60,
   '{"en": {"Name": "Window Cleaning", "Description": "Interior and exterior window cleaning with streak-free finish"}, "cs": {"Name": "Mytí oken", "Description": "Mytí oken zevnitř i zvenčí bez šmouh"}, "ru": {"Name": "Мытье окон", "Description": "Мытье окон изнутри и снаружи без разводов"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Carpet Cleaning', 'Professional carpet steam cleaning and stain removal',
   350.00, 100.00, 90,
   '{"en": {"Name": "Carpet Cleaning", "Description": "Professional carpet steam cleaning and stain removal"}, "cs": {"Name": "Čištění koberců", "Description": "Profesionální parní čištění koberců a odstraňování skvrn"}, "ru": {"Name": "Чистка ковров", "Description": "Профессиональная паровая чистка ковров и удаление пятен"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Upholstery Cleaning', 'Deep cleaning of sofas, chairs, and fabric furniture',
   450.00, 0.00, 75,
   '{"en": {"Name": "Upholstery Cleaning", "Description": "Deep cleaning of sofas, chairs, and fabric furniture"}, "cs": {"Name": "Čištění čalounění", "Description": "Hloubkové čištění sedaček, židlí a látkového nábytku"}, "ru": {"Name": "Чистка обивки", "Description": "Глубокая чистка диванов, кресел и тканевой мебели"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Post-Construction Cleanup', 'Specialized cleaning after renovation or construction work',
   1200.00, 300.00, 240,
   '{"en": {"Name": "Post-Construction Cleanup", "Description": "Specialized cleaning after renovation or construction work"}, "cs": {"Name": "Úklid po rekonstrukci", "Description": "Specializovaný úklid po rekonstrukci nebo stavebních pracích"}, "ru": {"Name": "Уборка после ремонта", "Description": "Специализированная уборка после ремонта или строительных работ"}}'),

  -- Premium Services
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Move-in/Move-out Cleaning', 'Complete cleaning for moving in or out of property',
   1000.00, 200.00, 180,
   '{"en": {"Name": "Move-in/Move-out Cleaning", "Description": "Complete cleaning for moving in or out of property"}, "cs": {"Name": "Úklid při stěhování", "Description": "Kompletní úklid při nastěhování nebo vystěhování z nemovitosti"}, "ru": {"Name": "Уборка при переезде", "Description": "Полная уборка при въезде или выезде из недвижимости"}}'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Eco-Friendly Cleaning', 'Green cleaning using only eco-friendly and non-toxic products',
   600.00, 180.00, 135,
   '{"en": {"Name": "Eco-Friendly Cleaning", "Description": "Green cleaning using only eco-friendly and non-toxic products"}, "cs": {"Name": "Ekologický úklid", "Description": "Zelený úklid používající pouze ekologické a netoxické produkty"}, "ru": {"Name": "Экологическая уборка", "Description": "Зеленая уборка с использованием только экологически чистых и нетоксичных продуктов"}}');

-- 7. PACKAGES
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

-- 8. USERS AND EMPLOYEES
INSERT INTO public."Users" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Password", "FirstName", "LastName",
  "Email", "PhoneNumber", "BirthDate", "Profile",
  "AuthenticationType", "IsEmailConfirmed", "PreferredLanguageCode"
)
VALUES
  -- Customer Users (Czech language by default)
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Jan', 'Novák', 'jan.novak@email.cz', '+420123456789', '1985-03-15', 2, 1, true, 'cs'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Marie', 'Svobodová', 'marie.svobodova@email.cz', '+420234567890', '1990-07-22', 2, 1, true, 'cs'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Petr', 'Dvořák', 'petr.dvorak@email.cz', '+420345678901', '1988-11-05', 2, 1, true, 'cs'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Anna', 'Černá', 'anna.cerna@email.cz', '+420456789012', '1992-04-18', 2, 1, true, 'cs'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Tomáš', 'Procházka', 'tomas.prochazka@email.cz', '+420567890123', '1987-09-12', 2, 1, true, 'cs'),

  -- Employee Users (Czech language for @cleansia.cz)
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Kateřina', 'Novotná', 'katerina.novotna@cleansia.cz', '+420678901234', '1993-06-08', 2, 1, true, 'cs'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Michal', 'Krejčí', 'michal.krejci@cleansia.cz', '+420789012345', '1991-12-03', 2, 1, true, 'cs'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Zuzana', 'Horáková', 'zuzana.horakova@cleansia.cz', '+420890123456', '1989-02-14', 2, 1, true, 'cs'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Pavel', 'Veselý', 'pavel.vesely@cleansia.cz', '+420901234567', '1986-08-27', 2, 1, true, 'cs'),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '$2a$11$LGWjlgYDdH1Zso.FvdZbkebhVtKj39L1HYN0GlbE3rRYcZw5I9RQ6', -- Password: Test123!
   'Lenka', 'Marková', 'lenka.markova@cleansia.cz', '+420012345678', '1994-05-19', 2, 1, true, 'cs');

-- 9. CARTS (Create carts with UserId after users exist)
INSERT INTO public."Carts" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn", "UserId"
)
VALUES
  -- Customer Carts
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'jan.novak@email.cz')),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'marie.svobodova@email.cz')),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'petr.dvorak@email.cz')),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'anna.cerna@email.cz')),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'tomas.prochazka@email.cz')),
  -- Employee Carts
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'katerina.novotna@cleansia.cz')),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'michal.krejci@cleansia.cz')),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'zuzana.horakova@cleansia.cz')),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'pavel.vesely@cleansia.cz')),
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'lenka.markova@cleansia.cz'));

INSERT INTO public."Employees" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "ICO", "IBAN", "AverageRating",
  "ComplaintsCount", "ContractStatus", "PassportId",
  "NationalityId", "EmergencyContactName", "EmergencyContactPhone",
  "Availability", "DocumentFileNames", "UserId"
)
VALUES
  -- Employee 1: Kateřina Novotná
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '87654321', 'CZ6508000000192000145399', 4.8, 0, 1, 'P123456789',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE'),
   'Jana Novotná', '+420777888999',
   '{"Monday":[{"Start":"09:00:00","End":"17:00:00"}],"Tuesday":[{"Start":"09:00:00","End":"17:00:00"}],"Wednesday":[{"Start":"09:00:00","End":"17:00:00"}],"Thursday":[{"Start":"09:00:00","End":"17:00:00"}],"Friday":[{"Start":"09:00:00","End":"17:00:00"}]}',
   '[]',
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'katerina.novotna@cleansia.cz')),

  -- Employee 2: Michal Krejčí
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '87654322', 'CZ6508000000192000145400', 4.6, 1, 1, 'P987654321',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE'),
   'Eva Krejčí', '+420777888998',
   '{"Monday":[{"Start":"08:00:00","End":"16:00:00"}],"Tuesday":[{"Start":"08:00:00","End":"16:00:00"}],"Wednesday":[{"Start":"08:00:00","End":"16:00:00"}],"Thursday":[{"Start":"08:00:00","End":"16:00:00"}],"Friday":[{"Start":"08:00:00","End":"16:00:00"}],"Saturday":[{"Start":"10:00:00","End":"14:00:00"}]}',
   '[]',
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'michal.krejci@cleansia.cz')),

  -- Employee 3: Zuzana Horáková
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '87654323', 'CZ6508000000192000145401', 4.9, 0, 1, 'P456789123',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE'),
   'Martin Horák', '+420777888997',
   '{"Monday":[{"Start":"10:00:00","End":"18:00:00"}],"Tuesday":[{"Start":"10:00:00","End":"18:00:00"}],"Wednesday":[{"Start":"10:00:00","End":"18:00:00"}],"Thursday":[{"Start":"10:00:00","End":"18:00:00"}],"Friday":[{"Start":"10:00:00","End":"18:00:00"}]}',
   '[]',
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'zuzana.horakova@cleansia.cz')),

  -- Employee 4: Pavel Veselý
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '87654324', 'CZ6508000000192000145402', 4.5, 2, 1, 'P789123456',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE'),
   'Petra Veselá', '+420777888996',
   '{"Monday":[{"Start":"07:00:00","End":"15:00:00"}],"Tuesday":[{"Start":"07:00:00","End":"15:00:00"}],"Wednesday":[{"Start":"07:00:00","End":"15:00:00"}],"Thursday":[{"Start":"07:00:00","End":"15:00:00"}],"Friday":[{"Start":"07:00:00","End":"15:00:00"}],"Saturday":[{"Start":"09:00:00","End":"13:00:00"}]}',
   '[]',
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'pavel.vesely@cleansia.cz')),

  -- Employee 5: Lenka Marková
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '87654325', 'CZ6508000000192000145403', 4.7, 0, 1, 'P321654987',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE'),
   'Tomáš Marek', '+420777888995',
   '{"Monday":[{"Start":"09:30:00","End":"17:30:00"}],"Tuesday":[{"Start":"09:30:00","End":"17:30:00"}],"Wednesday":[{"Start":"09:30:00","End":"17:30:00"}],"Thursday":[{"Start":"09:30:00","End":"17:30:00"}],"Friday":[{"Start":"09:30:00","End":"17:30:00"}],"Sunday":[{"Start":"12:00:00","End":"16:00:00"}]}',
   '[]',
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'lenka.markova@cleansia.cz'));

-- 10. ADDRESSES
INSERT INTO public."Addresses" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Street", "City", "ZipCode", "CountryId"
)
VALUES
  -- Customer Addresses in Prague
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Wenceslas Square 1', 'Prague', '11000',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Národní třída 25', 'Prague', '11000',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Vinohrady 456', 'Prague', '12000',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Karlínské náměstí 12', 'Prague', '18600',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Smíchov 789', 'Prague', '15000',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  -- Customer Addresses in Other Czech Cities
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Masarykova 567', 'Brno', '60200',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Stodolní 123', 'Ostrava', '70200',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Americká 45', 'Plzen', '30100',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  -- Employee Addresses
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Dejvická 321', 'Prague', '16000',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Anděl 654', 'Prague', '15000',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Náměstí Míru 987', 'Prague', '12000',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Florenc 147', 'Prague', '18600',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE')),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Waltrovka 258', 'Prague', '15000',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE'));

-- 11. ORDERS AND RELATED DATA
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

-- Insert Orders
INSERT INTO public."Orders" (
  "Id", "IsActive", "EmployeePayCalculated", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "CustomerName", "CustomerEmail",
  "CustomerPhone", "CustomerAddressId", "DisplayOrderNumber",
  "Rooms", "Bathrooms", "CleaningDateTime", "PaymentType",
  "PaymentStatus", "TotalPrice", "EstimatedTime",
  "ConfirmationCode", "StripeSessionId", "Notes",
  "SpecialInstructions", "AccessInstructions",
  "CurrencyId", "UserId", "EmployeeId", "Extras",
  "RequiredEmployees", "MaxEmployees"
)
VALUES
  -- Order 1: Jan Novák - Essential Clean (180 min = 2 employees required, 3 max)
  (generate_ulid()::TEXT, true, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Jan Novák', 'jan.novak@email.cz', '+420123456789',
   (SELECT "Id" FROM public."Addresses" WHERE "Street" = 'Wenceslas Square 1' LIMIT 1),
   'CLS-2025-0001', 3, 2, '2025-01-15 10:00:00', 1, 1, 1299.00, 180,
   'ABC123XYZ', 'cs_test_stripe_session_1',
   'Regular maintenance cleaning for family apartment',
   'Please be careful with the antique furniture in the living room',
   'Key under the mat, ring bell twice',
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'jan.novak@email.cz' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654321' LIMIT 1),
   '{"eco_products": true, "pet_friendly": false, "extra_vacuum": true}',
   2, 3),

  -- Order 2: Marie Svobodová - Deep Clean Premium (240 min = 2 employees required, 3 max)
  (generate_ulid()::TEXT, true, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Marie Svobodová', 'marie.svobodova@email.cz', '+420234567890',
   (SELECT "Id" FROM public."Addresses" WHERE "Street" = 'Národní třída 25' LIMIT 1),
   'CLS-2025-0002', 4, 3, '2025-01-16 09:00:00', 0, 0, 2199.00, 240,
   'DEF456ABC', 'cs_test_stripe_session_2',
   'Deep cleaning after renovation work',
   'There was recent painting work, please be extra careful with dust removal',
   'Security code: 1234, apartment 3B',
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'marie.svobodova@email.cz' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654322' LIMIT 1),
   '{"eco_products": false, "pet_friendly": true, "extra_vacuum": false}',
   2, 3),

  -- Order 3: Petr Dvořák - Moving Day Special (300 min = 3 employees required, 4 max)
  (generate_ulid()::TEXT, true, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Petr Dvořák', 'petr.dvorak@email.cz', '+420345678901',
   (SELECT "Id" FROM public."Addresses" WHERE "Street" = 'Vinohrady 456' LIMIT 1),
   'CLS-2025-0003', 2, 1, '2025-01-17 14:00:00', 1, 1, 2799.00, 300,
   'GHI789DEF', 'cs_test_stripe_session_3',
   'Move-out cleaning for apartment rental',
   'Need to return security deposit, please ensure everything is spotless',
   'Landlord will be present for inspection',
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'petr.dvorak@email.cz' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654323' LIMIT 1),
   '{"eco_products": true, "pet_friendly": false, "extra_vacuum": true}',
   3, 4),

  -- Order 4: Anna Černá - Kitchen & Bathroom Focus (150 min = 2 employees required, 3 max)
  (generate_ulid()::TEXT, true, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Anna Černá', 'anna.cerna@email.cz', '+420456789012',
   (SELECT "Id" FROM public."Addresses" WHERE "Street" = 'Karlínské náměstí 12' LIMIT 1),
   'CLS-2025-0004', 3, 2, '2025-01-18 11:00:00', 0, 0, 1399.00, 150,
   'JKL012GHI', 'cs_test_stripe_session_4',
   'Focus on kitchen and bathrooms only, other rooms are fine',
   'Kitchen has stubborn grease stains from cooking',
   'Use main entrance, elevator to 4th floor',
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'anna.cerna@email.cz' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654324' LIMIT 1),
   '{"eco_products": false, "pet_friendly": true, "extra_vacuum": false}',
   2, 3),

  -- Order 5: Tomáš Procházka - Eco-Green Package (210 min = 2 employees required, 3 max)
  (generate_ulid()::TEXT, true, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Tomáš Procházka', 'tomas.prochazka@email.cz', '+420567890123',
   (SELECT "Id" FROM public."Addresses" WHERE "Street" = 'Smíchov 789' LIMIT 1),
   'CLS-2025-0005', 5, 3, '2025-01-19 08:00:00', 1, 1, 1899.00, 210,
   'MNO345JKL', 'cs_test_stripe_session_5',
   'Eco-friendly cleaning for family with small children',
   'Please use only non-toxic products due to allergies',
   'Doorbell broken, please call when arriving',
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'tomas.prochazka@email.cz' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654325' LIMIT 1),
   '{"eco_products": true, "pet_friendly": true, "extra_vacuum": true}',
   2, 3),

  -- Order 6: Complete Home Clean (No package, individual services) (165 min = 2 employees required, 3 max)
  (generate_ulid()::TEXT, true, false, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Jan Novák', 'jan.novak@email.cz', '+420123456789',
   (SELECT "Id" FROM public."Addresses" WHERE "Street" = 'Wenceslas Square 1' LIMIT 1),
   'CLS-2025-0006', 3, 2, '2025-01-20 13:00:00', 0, 0, 1150.00, 165,
   'PQR678MNO', 'cs_test_stripe_session_6',
   'Follow-up cleaning with individual services',
   'Focus on areas missed in previous cleaning',
   'Same access as before',
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'jan.novak@email.cz' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654321' LIMIT 1),
   '{"eco_products": false, "pet_friendly": false, "extra_vacuum": false}',
   2, 3);

-- Insert Order Services (Junction table for orders and individual services)
INSERT INTO public."OrderServices" (
  "Id", "IsActive", "OrderId", "ServiceId"
)
VALUES
  -- Additional services for Order 6 (no package)
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0006' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'General Cleaning' LIMIT 1)),

  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0006' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Window Cleaning' LIMIT 1)),

  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0006' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Carpet Cleaning' LIMIT 1));

-- Insert Order Packages (Junction table for orders and individual packages)
INSERT INTO public."OrderPackages" (
  "Id", "IsActive", "OrderId", "PackageId"
)
VALUES
  -- Additional services for Order 6
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0006' LIMIT 1),
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Essential Clean' LIMIT 1)),

  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0006' LIMIT 1),
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Deep Clean Premium' LIMIT 1)),

  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0006' LIMIT 1),
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Moving Day Special' LIMIT 1));


-- Insert Order Status Tracks (Order history)
INSERT INTO public."OrderStatusHistory" (
  "Id", "IsActive","CreatedBy", "CreatedOn", "Status", "OrderId"
)
VALUES
  -- Order 1 Status History
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, 1, (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0001' LIMIT 1)),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, 2, (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0001' LIMIT 1)),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, 3, (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0001' LIMIT 1)),

  -- Order 2 Status History
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, 4, (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0002' LIMIT 1)),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, 1, (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0002' LIMIT 1)),

  -- Order 3 Status History
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, 1, (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0003' LIMIT 1)),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP,1, (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0003' LIMIT 1)),

  -- Order 4 Status History
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, 1, (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0004' LIMIT 1)),

  -- Order 5 Status History
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, 1, (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0005' LIMIT 1)),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, 1, (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0005' LIMIT 1)),

  -- Order 6 Status History
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, 1, (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0006' LIMIT 1));

-- 12. PAY PERIODS
-- Creating monthly pay periods for the last 3 months and upcoming month
INSERT INTO public."PayPeriods" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
  "StartDate", "EndDate", "Status", "ClosedAt", "ClosedBy", "PaidAt", "Notes"
)
VALUES
  -- December 2024 (Closed and Paid)
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '90 days', NULL, NULL, NULL, NULL,
   '2024-12-01 00:00:00', '2024-12-31 23:59:59', 2,
   '2025-01-02 10:00:00', 'admin@cleansia.cz', '2025-01-05 14:30:00',
   'December 2024 period - All invoices paid on time'),

  -- January 2025 (Closed but not yet paid)
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '60 days', NULL, NULL, NULL, NULL,
   '2025-01-01 00:00:00', '2025-01-31 23:59:59', 1,
   '2025-02-01 09:00:00', 'admin@cleansia.cz', NULL,
   'January 2025 period - Ready for payment processing'),

  -- February 2025 (Open - Current)
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '30 days', NULL, NULL, NULL, NULL,
   '2025-02-01 00:00:00', '2025-02-28 23:59:59', 0,
   NULL, NULL, NULL,
   'February 2025 period - Currently active'),

  -- March 2025 (Open - Future)
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   '2025-03-01 00:00:00', '2025-03-31 23:59:59', 0,
   NULL, NULL, NULL,
   'March 2025 period - Upcoming'),

  -- November 2024 (Closed and Paid - Historical)
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '120 days', NULL, NULL, NULL, NULL,
   '2024-11-01 00:00:00', '2024-11-30 23:59:59', 2,
   '2024-12-01 10:00:00', 'admin@cleansia.cz', '2024-12-05 15:00:00',
   'November 2024 period - Historical data');

-- 13. EMPLOYEE PAY CONFIG
-- INSERT EMPLOYEE PAY CONFIG
-- Setting up pay configurations for each employee for different service/package combinations
INSERT INTO public."EmployeePayConfigs" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
  "ServiceId", "PackageId", "BasePay", "ExtraPerRoom", "ExtraPerBathroom",
  "DistanceRatePerKm", "MinimumPay", "MaximumPay", "CurrencyId", "Description"
)
VALUES
  -- Kateřina Novotná - General Cleaning
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '90 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'General Cleaning' LIMIT 1),
   NULL, 350.00, 80.00, 50.00, 5.00, 300.00, 1500.00,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   'Standard pay rate for general cleaning services'),

  -- Kateřina Novotná - Essential Clean Package
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '90 days', NULL, NULL, NULL, NULL,
   NULL,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Essential Clean' LIMIT 1),
   400.00, 100.00, 60.00, 5.00, 350.00, 1800.00,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   'Package rate for essential clean'),

  -- Michal Krejčí - Deep Cleaning
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '85 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Deep Cleaning' LIMIT 1),
   NULL, 500.00, 120.00, 80.00, 6.00, 400.00, 2000.00,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   'Premium rate for deep cleaning expertise'),

  -- Michal Krejčí - Deep Clean Premium Package
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '85 days', NULL, NULL, NULL, NULL,
   NULL,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Deep Clean Premium' LIMIT 1),
   600.00, 150.00, 100.00, 6.00, 500.00, 2500.00,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   'Premium package rate for deep clean services'),

  -- Zuzana Horáková - Move-in/Move-out Cleaning
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '80 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Move-in/Move-out Cleaning' LIMIT 1),
   NULL, 550.00, 130.00, 90.00, 7.00, 450.00, 2200.00,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   'Specialist rate for move-in/out cleaning'),

  -- Zuzana Horáková - Moving Day Special Package
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '80 days', NULL, NULL, NULL, NULL,
   NULL,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Moving Day Special' LIMIT 1),
   700.00, 180.00, 110.00, 7.00, 600.00, 2800.00,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   'Special package rate for moving day services'),

  -- Pavel Veselý - Kitchen Deep Clean
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '75 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Kitchen Deep Clean' LIMIT 1),
   NULL, 300.00, 0.00, 40.00, 5.00, 250.00, 1200.00,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   'Specialist rate for kitchen cleaning'),

  -- Pavel Veselý - Kitchen & Bathroom Focus Package
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '75 days', NULL, NULL, NULL, NULL,
   NULL,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Kitchen & Bathroom Focus' LIMIT 1),
   450.00, 0.00, 80.00, 5.00, 400.00, 1600.00,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   'Package rate for kitchen and bathroom focus'),

  -- Lenka Marková - Eco-Friendly Cleaning
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '70 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Eco-Friendly Cleaning' LIMIT 1),
   NULL, 400.00, 100.00, 70.00, 6.00, 350.00, 1700.00,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   'Eco-specialist rate with premium for green products'),

  -- Lenka Marková - Eco-Green Package
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '70 days', NULL, NULL, NULL, NULL,
   NULL,
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Eco-Green Package' LIMIT 1),
   550.00, 120.00, 90.00, 6.00, 450.00, 2000.00,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   'Eco-friendly package with premium rate');

-- 14. ORDER EMPLOYEE PAY
-- INSERT ORDER EMPLOYEE PAY
-- Creating pay records for completed orders
INSERT INTO public."OrderEmployeePays" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
  "OrderId", "EmployeeId", "PayPeriodId", "BasePay", "ExtrasPay",
  "ExpensesPay", "BonusPay", "DeductionPay", "TotalPay",
  "PayBreakdown", "IsApproved"
)
VALUES
  -- Order 1 (CLS-2025-0001) - Kateřina Novotná - January period
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '20 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0001' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654321' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2025-01-01 00:00:00' LIMIT 1),
   400.00, 260.00, 50.00, 100.00, 0.00, 810.00,
   '{"basePay": 400, "roomExtras": 160, "bathroomExtras": 100, "travelExpenses": 50, "qualityBonus": 100}',
   true),

  -- Order 2 (CLS-2025-0002) - Michal Krejčí - January period
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '19 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0002' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654322' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2025-01-01 00:00:00' LIMIT 1),
   600.00, 500.00, 80.00, 150.00, 0.00, 1330.00,
   '{"basePay": 600, "roomExtras": 300, "bathroomExtras": 200, "travelExpenses": 80, "qualityBonus": 150}',
   true),

  -- Order 3 (CLS-2025-0003) - Zuzana Horáková - January period
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '18 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0003' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654323' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2025-01-01 00:00:00' LIMIT 1),
   700.00, 290.00, 100.00, 200.00, 0.00, 1290.00,
   '{"basePay": 700, "roomExtras": 180, "bathroomExtras": 110, "travelExpenses": 100, "performanceBonus": 200}',
   true),

  -- Order 4 (CLS-2025-0004) - Pavel Veselý - January period
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '17 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0004' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654324' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2025-01-01 00:00:00' LIMIT 1),
   450.00, 80.00, 40.00, 0.00, 50.00, 520.00,
   '{"basePay": 450, "bathroomExtras": 80, "travelExpenses": 40, "lateArrivalDeduction": -50}',
   true),

  -- Order 5 (CLS-2025-0005) - Lenka Marková - January period
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '16 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0005' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654325' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2025-01-01 00:00:00' LIMIT 1),
   550.00, 510.00, 60.00, 120.00, 0.00, 1240.00,
   '{"basePay": 550, "roomExtras": 360, "bathroomExtras": 150, "travelExpenses": 60, "ecoProductBonus": 120}',
   true),

  -- Order 6 (CLS-2025-0006) - Kateřina Novotná - February period
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '10 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0006' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654321' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2025-02-01 00:00:00' LIMIT 1),
   350.00, 240.00, 45.00, 80.00, 0.00, 715.00,
   '{"basePay": 350, "roomExtras": 160, "bathroomExtras": 80, "travelExpenses": 45, "repeatClientBonus": 80}',
   true);

-- 15. EMPLOYEE INVOICES
-- INSERT EMPLOYEE INVOICES
-- Creating invoices for employees based on their completed work
INSERT INTO public."EmployeeInvoices" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
  "EmployeeId", "PayPeriodId", "InvoiceNumber", "TotalOrders",
  "SubTotal", "BonusAmount", "DeductionAmount", "TotalAmount",
  "CurrencyId", "Status", "PdfBlobUrl", "GeneratedAt",
  "ApprovedAt", "ApprovedBy", "PaidAt", "AdminNotes",
  "VariableSymbol", "SpecificSymbol", "BankTransferNote"
)
VALUES
  -- Invoice 1: Kateřina Novotná - January 2025 (Approved, not paid yet)
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '15 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654321' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2025-01-01 00:00:00' LIMIT 1),
   'INV-202501-KN001', 1, 710.00, 100.00, 0.00, 810.00,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   1, NULL, CURRENT_TIMESTAMP - INTERVAL '15 days',
   CURRENT_TIMESTAMP - INTERVAL '10 days', 'admin@cleansia.cz', NULL,
   'Excellent performance in January. Quality bonus awarded.',
   '0321876543', NULL, NULL),

  -- Invoice 2: Michal Krejčí - January 2025 (Approved, not paid yet)
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '15 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654322' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2025-01-01 00:00:00' LIMIT 1),
   'INV-202501-MK001', 1, 1180.00, 150.00, 0.00, 1330.00,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   1, NULL, CURRENT_TIMESTAMP - INTERVAL '15 days',
   CURRENT_TIMESTAMP - INTERVAL '10 days', 'admin@cleansia.cz', NULL,
   'Deep cleaning work completed to high standard.',
   '0322987654', NULL, NULL),

  -- Invoice 3: Zuzana Horáková - January 2025 (Paid)
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '15 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654323' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2025-01-01 00:00:00' LIMIT 1),
   'INV-202501-ZH001', 1, 1090.00, 200.00, 0.00, 1290.00,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   2, 'https://storage.cleansia.cz/invoices/2025/01/inv-zh001.pdf',
   CURRENT_TIMESTAMP - INTERVAL '15 days',
   CURRENT_TIMESTAMP - INTERVAL '10 days', 'admin@cleansia.cz',
   CURRENT_TIMESTAMP - INTERVAL '5 days',
   'Move-out cleaning excellent. Performance bonus granted. Paid via bank transfer.',
   '0323098765', '2501', 'Payment for Invoice INV-202501-ZH001'),

  -- Invoice 4: Pavel Veselý - January 2025 (Approved, not paid yet)
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '15 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654324' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2025-01-01 00:00:00' LIMIT 1),
   'INV-202501-PV001', 1, 570.00, 0.00, 50.00, 520.00,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   1, NULL, CURRENT_TIMESTAMP - INTERVAL '15 days',
   CURRENT_TIMESTAMP - INTERVAL '10 days', 'admin@cleansia.cz', NULL,
   'Good work, but late arrival noted. Minor deduction applied.',
   '0324109876', NULL, NULL),

  -- Invoice 5: Lenka Marková - January 2025 (Approved, not paid yet)
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '15 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654325' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2025-01-01 00:00:00' LIMIT 1),
   'INV-202501-LM001', 1, 1120.00, 120.00, 0.00, 1240.00,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   1, NULL, CURRENT_TIMESTAMP - INTERVAL '15 days',
   CURRENT_TIMESTAMP - INTERVAL '10 days', 'admin@cleansia.cz', NULL,
   'Eco-friendly cleaning done professionally. Bonus for using green products.',
   '0325210987', NULL, NULL),

  -- Invoice 6: Kateřina Novotná - February 2025 (Pending approval)
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '5 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654321' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2025-02-01 00:00:00' LIMIT 1),
   'INV-202502-KN001', 1, 635.00, 80.00, 0.00, 715.00,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   0, NULL, CURRENT_TIMESTAMP - INTERVAL '5 days',
   NULL, NULL, NULL,
   'February work in progress. Awaiting approval.',
   '0321987654', NULL, NULL),

  -- Invoice 7: Kateřina Novotná - December 2024 (Paid - Historical)
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '60 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654321' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2024-12-01 00:00:00' LIMIT 1),
   'INV-202412-KN001', 8, 6800.00, 500.00, 0.00, 7300.00,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   2, 'https://storage.cleansia.cz/invoices/2024/12/inv-kn001.pdf',
   CURRENT_TIMESTAMP - INTERVAL '60 days',
   CURRENT_TIMESTAMP - INTERVAL '58 days', 'admin@cleansia.cz',
   CURRENT_TIMESTAMP - INTERVAL '55 days',
   'Excellent month with 8 completed orders. Monthly bonus granted.',
   '0321765432', '2412', 'Payment for Invoice INV-202412-KN001'),

  -- Invoice 8: Michal Krejčí - December 2024 (Paid - Historical)
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '60 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654322' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2024-12-01 00:00:00' LIMIT 1),
   'INV-202412-MK001', 6, 7200.00, 400.00, 100.00, 7500.00,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   2, 'https://storage.cleansia.cz/invoices/2024/12/inv-mk001.pdf',
   CURRENT_TIMESTAMP - INTERVAL '60 days',
   CURRENT_TIMESTAMP - INTERVAL '58 days', 'admin@cleansia.cz',
   CURRENT_TIMESTAMP - INTERVAL '55 days',
   'Good performance. One customer complaint resulted in minor deduction.',
   '0322876543', '2412', 'Payment for Invoice INV-202412-MK001');

-- ============================================================
-- INVOICE TEMPLATES
-- ============================================================
INSERT INTO public."InvoiceTemplates" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
  "TemplateName", "CountryId", "LanguageId", "Version",
  "BlobUrl", "ActivatedAt", "Description"
)
VALUES
  -- Czech Republic + English
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'StandardInvoice',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1),
   (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en' LIMIT 1),
   1, 'invoice-templates/cze/en/standard-v1.html', CURRENT_TIMESTAMP,
   'Default English invoice template for Czech Republic'),

  -- Czech Republic + Czech
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'StandardInvoice',
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1),
   (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs' LIMIT 1),
   1, 'invoice-templates/cze/cs/standard-v1.html', CURRENT_TIMESTAMP,
   'Výchozí česká šablona faktury pro Českou republiku');

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
-- COMPANY INFO
-- ============================================================
INSERT INTO public."CompanyInfo" (
    "Id", "LegalName", "TradingName", "Tagline",
    "RegistrationNumber", "VatNumber",
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
-- Password Reset - English
(generate_ulid()::TEXT, true, 1, 'Subject', 'Reset Your Password', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'Greeting', 'Hello', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'IntroText', 'You requested to reset your password. Click the button below to proceed:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'ButtonText', 'Reset Password', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'AlternativeText', 'Or use this verification code:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'ExpiryNotice', 'This link will expire in 24 hours.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'IgnoreText', 'If you did not request this, please ignore this email.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SupportText', 'Need help? Contact us at', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SupportEmail', 'support@cleansia.com', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'Closing', 'Best regards,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'TeamName', 'The Cleansia Team', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'FooterText', '© 2025 Cleansia. All rights reserved.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Password Reset - Czech
(generate_ulid()::TEXT, true, 1, 'Subject', 'Obnovení hesla', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'Greeting', 'Dobrý den', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'IntroText', 'Požádali jste o obnovení hesla. Klikněte na tlačítko níže pro pokračování:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'ButtonText', 'Obnovit heslo', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'AlternativeText', 'Nebo použijte tento ověřovací kód:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'ExpiryNotice', 'Platnost tohoto odkazu vyprší za 24 hodin.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'IgnoreText', 'Pokud jste o toto nepožádali, ignorujte prosím tento e-mail.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SupportText', 'Potřebujete pomoc? Kontaktujte nás na', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'SupportEmail', 'support@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'Closing', 'S pozdravem,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'TeamName', 'Tým Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 1, 'FooterText', '© 2025 Cleansia. Všechna práva vyhrazena.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Order Receipt - English
(generate_ulid()::TEXT, true, 2, 'Subject', 'Your Order Receipt', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'Greeting', 'Dear', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'ThankYouText', 'Thank you for your order! We are pleased to confirm your booking.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'OrderDetailsTitle', 'Order Details', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'OrderNumberLabel', 'Order Number:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'OrderDateLabel', 'Order Date:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'TotalAmountLabel', 'Total Amount:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'AttachmentText', 'Please find your detailed receipt attached to this email.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'TrackOrderText', 'You can track your order status at any time:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'ButtonText', 'View Order Status', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'QuestionsText', 'If you have any questions about your order, please don''t hesitate to contact us.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'SupportText', 'Contact us at', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'SupportEmail', 'support@cleansia.com', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'Closing', 'Best regards,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'TeamName', 'The Cleansia Team', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'FooterText', '© 2025 Cleansia. All rights reserved.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Order Receipt - Czech
(generate_ulid()::TEXT, true, 2, 'Subject', 'Potvrzení objednávky', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'Greeting', 'Vážený zákazníku', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'ThankYouText', 'Děkujeme za Vaši objednávku! S potěšením potvrzujeme Vaši rezervaci.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'OrderDetailsTitle', 'Detaily objednávky', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'OrderNumberLabel', 'Číslo objednávky:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'OrderDateLabel', 'Datum objednávky:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'TotalAmountLabel', 'Celková částka:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'AttachmentText', 'Detailní účtenku naleznete v příloze tohoto e-mailu.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'TrackOrderText', 'Stav objednávky můžete kdykoliv sledovat:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'ButtonText', 'Zobrazit stav objednávky', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'QuestionsText', 'Pokud máte jakékoliv dotazy ohledně Vaší objednávky, neváhejte nás kontaktovat.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'SupportText', 'Kontaktujte nás na', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'SupportEmail', 'support@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'Closing', 'S pozdravem,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'TeamName', 'Tým Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 2, 'FooterText', '© 2025 Cleansia. Všechna práva vyhrazena.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Email Confirmation - English
(generate_ulid()::TEXT, true, 3, 'Subject', 'Confirm Your Email Address', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'Greeting', 'Welcome', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'IntroText', 'Thank you for registering with Cleansia! To complete your registration, please verify your email address.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'InstructionsText', 'Use the verification code below to confirm your email:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'CodeLabel', 'Verification Code:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'ExpiryNotice', 'This code will expire in 24 hours.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'SecurityText', 'For security reasons, do not share this code with anyone.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'IgnoreText', 'If you did not create an account, please ignore this email.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'SupportText', 'Need help? Contact us at', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'SupportEmail', 'support@cleansia.com', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'Closing', 'Best regards,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'TeamName', 'The Cleansia Team', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'FooterText', '© 2025 Cleansia. All rights reserved.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Email Confirmation - Czech
(generate_ulid()::TEXT, true, 3, 'Subject', 'Potvrďte Vaši e-mailovou adresu', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'Greeting', 'Vítejte', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'IntroText', 'Děkujeme za registraci v Cleansia! Pro dokončení registrace prosím ověřte Vaši e-mailovou adresu.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'InstructionsText', 'Použijte níže uvedený ověřovací kód pro potvrzení e-mailu:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'CodeLabel', 'Ověřovací kód:', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'ExpiryNotice', 'Platnost tohoto kódu vyprší za 24 hodin.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'SecurityText', 'Z bezpečnostních důvodů tento kód s nikým nesdílejte.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'IgnoreText', 'Pokud jste účet nevytvářeli, ignorujte prosím tento e-mail.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'SupportText', 'Potřebujete pomoc? Kontaktujte nás na', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'SupportEmail', 'support@cleansia.cz', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'Closing', 'S pozdravem,', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'TeamName', 'Tým Cleansia', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
(generate_ulid()::TEXT, true, 3, 'FooterText', '© 2025 Cleansia. Všechna práva vyhrazena.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Pay Period Closed - English
(generate_ulid()::TEXT, true, 4, 'Subject', 'Pay Period Closed', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
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
(generate_ulid()::TEXT, true, 4, 'FooterText', '© 2024 Cleansia. All rights reserved.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Pay Period Closed - Czech
(generate_ulid()::TEXT, true, 4, 'Subject', 'Platební období uzavřeno', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
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
(generate_ulid()::TEXT, true, 4, 'FooterText', '© 2024 Cleansia. Všechna práva vyhrazena.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Period End Reminder - English
(generate_ulid()::TEXT, true, 5, 'Subject', 'Pay Period Ending Soon', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
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
(generate_ulid()::TEXT, true, 5, 'FooterText', '© 2024 Cleansia. All rights reserved.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),

-- Period End Reminder - Czech
(generate_ulid()::TEXT, true, 5, 'Subject', 'Platební období brzy končí', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL),
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
(generate_ulid()::TEXT, true, 5, 'FooterText', '© 2024 Cleansia. Všechna práva vyhrazena.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL);

-- ============================================================
-- RECEIPT TEMPLATES
-- ============================================================
INSERT INTO public."ReceiptTemplates" (
    "Id", "TemplateName", "CountryId", "LanguageId", "Version",
    "BlobUrl", "IsActive", "ActivatedAt", "Description",
    "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn"
)
VALUES
-- Czech Receipt Template
(generate_ulid()::TEXT,
 'Standard Receipt Template v1',
 (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1),
 (SELECT "Id" FROM public."Languages" WHERE "Code" = 'cs' LIMIT 1),
 1,
 'receipt-templates/receipt-template-v1.html',
 true,
 CURRENT_TIMESTAMP,
 'Professional receipt template with company information, order details, and payment breakdown for Czech customers.',
 'system',
 CURRENT_TIMESTAMP,
 'system',
 CURRENT_TIMESTAMP),

-- English Receipt Template
(generate_ulid()::TEXT,
 'Standard Receipt Template v1 (English)',
 (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1),
 (SELECT "Id" FROM public."Languages" WHERE "Code" = 'en' LIMIT 1),
 1,
 'receipt-templates/receipt-template-v1.html',
 true,
 CURRENT_TIMESTAMP,
 'Professional receipt template with company information, order details, and payment breakdown for English-speaking customers.',
 'system',
 CURRENT_TIMESTAMP,
 'system',
 CURRENT_TIMESTAMP);

-- FIX EMPLOYEE ADDRESS CONNECTIONS
-- Create proper residential addresses for employees and connect them
INSERT INTO public."Addresses" (
    "Id", "IsActive", "CreatedBy", "CreatedOn",
    "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
    "Street", "City", "ZipCode", "CountryId"
)
VALUES
    -- Employee Residential Address 1: Kateřina Novotná
    (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
     'Vinohrady 15', 'Prague 2', '12000',
     (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1)),

    -- Employee Residential Address 2: Michal Krejčí
    (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
     'Karlín 28', 'Prague 8', '18600',
     (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1)),

    -- Employee Residential Address 3: Zuzana Horáková
    (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
     'Smíchov 42', 'Prague 5', '15000',
     (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1)),

    -- Employee Residential Address 4: Pavel Veselý
    (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
     'Břevnov 67', 'Prague 6', '16900',
     (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1)),

    -- Employee Residential Address 5: Lenka Marková
    (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
     'Dejvice 89', 'Prague 6', '16000',
     (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1));

-- Update Employee records to link them to their residential addresses
UPDATE public."Employees" 
SET "AddressId" = (
    SELECT a."Id" 
    FROM public."Addresses" a 
    WHERE a."Street" = CASE 
        WHEN EXISTS (SELECT 1 FROM public."Users" u WHERE u."Id" = public."Employees"."UserId" AND u."Email" = 'katerina.novotna@cleansia.cz') THEN 'Vinohrady 15'
        WHEN EXISTS (SELECT 1 FROM public."Users" u WHERE u."Id" = public."Employees"."UserId" AND u."Email" = 'michal.krejci@cleansia.cz') THEN 'Karlín 28'
        WHEN EXISTS (SELECT 1 FROM public."Users" u WHERE u."Id" = public."Employees"."UserId" AND u."Email" = 'zuzana.horakova@cleansia.cz') THEN 'Smíchov 42'
        WHEN EXISTS (SELECT 1 FROM public."Users" u WHERE u."Id" = public."Employees"."UserId" AND u."Email" = 'pavel.vesely@cleansia.cz') THEN 'Břevnov 67'
        WHEN EXISTS (SELECT 1 FROM public."Users" u WHERE u."Id" = public."Employees"."UserId" AND u."Email" = 'lenka.markova@cleansia.cz') THEN 'Dejvice 89'
    END
    AND a."CreatedOn" >= CURRENT_TIMESTAMP - INTERVAL '1 minute'  -- Only newly created addresses
    LIMIT 1
)
WHERE "UserId" IN (
    SELECT "Id" FROM public."Users" WHERE "Email" LIKE '%@cleansia.cz'
);

-- ============================================================
-- DISPUTES
-- ============================================================
-- Dispute seed data has been moved to insert_disputes.sql
-- Run that script separately after this one to populate disputes

-- Re-enable foreign key constraints
SET session_replication_role = DEFAULT;

COMMIT;
