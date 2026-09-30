-- ============================================================
-- PRODUCTION REFERENCE DATA — the skeleton a freshly migrated database needs
-- ============================================================
-- The operating company, the five languages, every country (Czechia serviced), the Czech service
-- cities, CZK, Czechia's market configuration and its operator, its invoice configuration and cleaner
-- document requirements, the e-mail template copy, the loyalty tiers and the Czech size ladder: the
-- rows no admin screen can create, and without which nobody can register or book.
--
-- Deliberately absent, because the admin console types them in from the owner's launch values sheet
-- (decision 77): the catalogue and its CZK prices, cleaner pay rates, Cleansia Plus plans with their
-- live Stripe Price ids, the company record (CompanyInfo), promo codes and every user. The CZK loyalty
-- divisor and apology credit and the tier thresholds and discounts below are the documented defaults
-- (/product/business-rules); the sheet's figures replace them on the currency form and the loyalty
-- tier page.
--
-- One transaction, and idempotent: a row that is already there is left as it is, so a second run
-- changes nothing and keeps whatever the console has edited since.
--
-- Production: runbook step P7 (deploy/AZURE-DEV-RUNBOOK.md), run by the owner and never by an agent.
-- Development: the host runs this file, then insert_seed_data.sql (the DEV fixtures), then
-- insert_local_dev_admin.sql (CleansiaStartupBase.DevelopmentSeedScripts).
-- ProductionBootstrapScriptTests runs it on an empty migrated database, twice, and books on it.

BEGIN TRANSACTION;

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

-- 2. TENANTS
-- The operating companies under the holding (ADR-0061 D1). The first is the Czech s.r.o.; every
-- stamped row below carries its id, and the CZE configuration names it as the market's operator.
-- A second company is a second row here plus an OperatorTenantId on its country's configuration.
INSERT INTO public."Tenants" ("Id", "IsActive", "Name", "CreatedBy", "CreatedOn")
VALUES ('cleansia-cz', true, 'Cleansia CZ s.r.o.', 'seed', now())
ON CONFLICT ("Id") DO NOTHING;

-- 2b. LANGUAGES
INSERT INTO public."Languages" (
  "Id", "IsActive", "Code", "Name"
)
SELECT generate_ulid()::TEXT, true, v.code, v.name
FROM (VALUES
  ('en', 'English'),
  ('cs', 'Čeština'),
  ('sk', 'Slovenčina'),
  ('uk', 'Українська'),
  ('ru', 'Русский')
) AS v(code, name)
WHERE NOT EXISTS (SELECT 1 FROM public."Languages" l WHERE l."Code" = v.code);

-- 3. COUNTRIES
-- The complete ISO 3166-1 set. IsServiced flips a country into the
-- customer/partner-facing pickers (driven by Country/GetServiced) — only the
-- country we actually operate in is seeded as serviced; admins flip the others
-- on from the Service Area page when expanding. IsActive is the separate
-- admin-catalog flag and stays true for all of them.
INSERT INTO public."Countries" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Name", "IsoCode", "IsoAlpha2", "Translations", "IsServiced"
)
SELECT generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
       v.name, v.iso_code, v.iso_alpha2, v.translations, v.is_serviced
FROM (VALUES
  ('Aruba', 'ABW', 'AW', '{"en": {"Name": "Aruba", "Description": "Caribbean country"}, "cs": {"Name": "Aruba", "Description": "Karibská země"}, "sk": {"Name": "Aruba", "Description": "Karibská krajina"}, "uk": {"Name": "Аруба", "Description": "Карибська країна"}, "ru": {"Name": "Аруба", "Description": "Карибская страна"}}', false),
  ('Afghanistan', 'AFG', 'AF', '{"en": {"Name": "Afghanistan", "Description": "South Asian country"}, "cs": {"Name": "Afghánistán", "Description": "Jihoasijská země"}, "sk": {"Name": "Afganistan", "Description": "Juhoázijská krajina"}, "uk": {"Name": "Афганістан", "Description": "Південноазійська країна"}, "ru": {"Name": "Афганистан", "Description": "Южноазиатская страна"}}', false),
  ('Angola', 'AGO', 'AO', '{"en": {"Name": "Angola", "Description": "Central African country"}, "cs": {"Name": "Angola", "Description": "Středoafrická země"}, "sk": {"Name": "Angola", "Description": "Stredoafrická krajina"}, "uk": {"Name": "Ангола", "Description": "Центральноафриканська країна"}, "ru": {"Name": "Ангола", "Description": "Центральноафриканская страна"}}', false),
  ('Anguilla', 'AIA', 'AI', '{"en": {"Name": "Anguilla", "Description": "Caribbean country"}, "cs": {"Name": "Anguilla", "Description": "Karibská země"}, "sk": {"Name": "Anguilla", "Description": "Karibská krajina"}, "uk": {"Name": "Ангілья", "Description": "Карибська країна"}, "ru": {"Name": "Ангилья", "Description": "Карибская страна"}}', false),
  ('Åland Islands', 'ALA', 'AX', '{"en": {"Name": "Åland Islands", "Description": "Northern European country"}, "cs": {"Name": "Ålandy", "Description": "Severní evropská země"}, "sk": {"Name": "Alandy", "Description": "Severná európska krajina"}, "uk": {"Name": "Аландські Острови", "Description": "Північноєвропейська країна"}, "ru": {"Name": "Аландские о-ва", "Description": "Северная европейская страна"}}', false),
  ('Albania', 'ALB', 'AL', '{"en": {"Name": "Albania", "Description": "Southeastern European country"}, "cs": {"Name": "Albánie", "Description": "Jihovýchodní evropská země"}, "sk": {"Name": "Albánsko", "Description": "Juhovýchodná európska krajina"}, "uk": {"Name": "Албанія", "Description": "Південно-східна європейська країна"}, "ru": {"Name": "Албания", "Description": "Юго-восточная европейская страна"}}', false),
  ('Andorra', 'AND', 'AD', '{"en": {"Name": "Andorra", "Description": "Southern European country"}, "cs": {"Name": "Andorra", "Description": "Jihoevropská země"}, "sk": {"Name": "Andorra", "Description": "Juhoeurópska krajina"}, "uk": {"Name": "Андорра", "Description": "Південноєвропейська країна"}, "ru": {"Name": "Андорра", "Description": "Южноевропейская страна"}}', false),
  ('United Arab Emirates', 'ARE', 'AE', '{"en": {"Name": "United Arab Emirates", "Description": "Western Asian country"}, "cs": {"Name": "Spojené arabské emiráty", "Description": "Západoasijská země"}, "sk": {"Name": "Spojené arabské emiráty", "Description": "Západoázijská krajina"}, "uk": {"Name": "Обʼєднані Арабські Емірати", "Description": "Західноазійська країна"}, "ru": {"Name": "ОАЭ", "Description": "Западноазиатская страна"}}', false),
  ('Argentina', 'ARG', 'AR', '{"en": {"Name": "Argentina", "Description": "South American country"}, "cs": {"Name": "Argentina", "Description": "Jihoamerická země"}, "sk": {"Name": "Argentína", "Description": "Juhoamerická krajina"}, "uk": {"Name": "Аргентина", "Description": "Південноамериканська країна"}, "ru": {"Name": "Аргентина", "Description": "Южноамериканская страна"}}', false),
  ('Armenia', 'ARM', 'AM', '{"en": {"Name": "Armenia", "Description": "Western Asian country"}, "cs": {"Name": "Arménie", "Description": "Západoasijská země"}, "sk": {"Name": "Arménsko", "Description": "Západoázijská krajina"}, "uk": {"Name": "Вірменія", "Description": "Західноазійська країна"}, "ru": {"Name": "Армения", "Description": "Западноазиатская страна"}}', false),
  ('American Samoa', 'ASM', 'AS', '{"en": {"Name": "American Samoa", "Description": "Oceanic country"}, "cs": {"Name": "Americká Samoa", "Description": "Oceánská země"}, "sk": {"Name": "Americká Samoa", "Description": "Oceánska krajina"}, "uk": {"Name": "Американське Самоа", "Description": "Океанійська країна"}, "ru": {"Name": "Американское Самоа", "Description": "Океанская страна"}}', false),
  ('Antarctica', 'ATA', 'AQ', '{"en": {"Name": "Antarctica", "Description": "Antarctic territory"}, "cs": {"Name": "Antarktida", "Description": "Antarktické území"}, "sk": {"Name": "Antarktída", "Description": "Antarktické územie"}, "uk": {"Name": "Антарктика", "Description": "Антарктична територія"}, "ru": {"Name": "Антарктида", "Description": "Антарктическая территория"}}', false),
  ('French Southern Territories', 'ATF', 'TF', '{"en": {"Name": "French Southern Territories", "Description": "Antarctic territory"}, "cs": {"Name": "Francouzská jižní území", "Description": "Antarktické území"}, "sk": {"Name": "Francúzske južné a antarktické územia", "Description": "Antarktické územie"}, "uk": {"Name": "Французькі Південні Території", "Description": "Антарктична територія"}, "ru": {"Name": "Французские Южные территории", "Description": "Антарктическая территория"}}', false),
  ('Antigua & Barbuda', 'ATG', 'AG', '{"en": {"Name": "Antigua & Barbuda", "Description": "Caribbean country"}, "cs": {"Name": "Antigua a Barbuda", "Description": "Karibská země"}, "sk": {"Name": "Antigua a Barbuda", "Description": "Karibská krajina"}, "uk": {"Name": "Антигуа і Барбуда", "Description": "Карибська країна"}, "ru": {"Name": "Антигуа и Барбуда", "Description": "Карибская страна"}}', false),
  ('Australia', 'AUS', 'AU', '{"en": {"Name": "Australia", "Description": "Oceanic country"}, "cs": {"Name": "Austrálie", "Description": "Oceánská země"}, "sk": {"Name": "Austrália", "Description": "Oceánska krajina"}, "uk": {"Name": "Австралія", "Description": "Океанійська країна"}, "ru": {"Name": "Австралия", "Description": "Океанская страна"}}', false),
  ('Austria', 'AUT', 'AT', '{"en": {"Name": "Austria", "Description": "Central European country"}, "cs": {"Name": "Rakousko", "Description": "Středoevropská země"}, "sk": {"Name": "Rakúsko", "Description": "Stredoeurópska krajina"}, "uk": {"Name": "Австрія", "Description": "Центральноєвропейська країна"}, "ru": {"Name": "Австрия", "Description": "Центральноевропейская страна"}}', false),
  ('Azerbaijan', 'AZE', 'AZ', '{"en": {"Name": "Azerbaijan", "Description": "Western Asian country"}, "cs": {"Name": "Ázerbájdžán", "Description": "Západoasijská země"}, "sk": {"Name": "Azerbajdžan", "Description": "Západoázijská krajina"}, "uk": {"Name": "Азербайджан", "Description": "Західноазійська країна"}, "ru": {"Name": "Азербайджан", "Description": "Западноазиатская страна"}}', false),
  ('Burundi', 'BDI', 'BI', '{"en": {"Name": "Burundi", "Description": "East African country"}, "cs": {"Name": "Burundi", "Description": "Východoafrická země"}, "sk": {"Name": "Burundi", "Description": "Východoafrická krajina"}, "uk": {"Name": "Бурунді", "Description": "Східноафриканська країна"}, "ru": {"Name": "Бурунди", "Description": "Восточноафриканская страна"}}', false),
  ('Belgium', 'BEL', 'BE', '{"en": {"Name": "Belgium", "Description": "Western European country"}, "cs": {"Name": "Belgie", "Description": "Západoevropská země"}, "sk": {"Name": "Belgicko", "Description": "Západoeurópska krajina"}, "uk": {"Name": "Бельгія", "Description": "Західноєвропейська країна"}, "ru": {"Name": "Бельгия", "Description": "Западноевропейская страна"}}', false),
  ('Benin', 'BEN', 'BJ', '{"en": {"Name": "Benin", "Description": "West African country"}, "cs": {"Name": "Benin", "Description": "Západoafrická země"}, "sk": {"Name": "Benin", "Description": "Západoafrická krajina"}, "uk": {"Name": "Бенін", "Description": "Західноафриканська країна"}, "ru": {"Name": "Бенин", "Description": "Западноафриканская страна"}}', false),
  ('Caribbean Netherlands', 'BES', 'BQ', '{"en": {"Name": "Caribbean Netherlands", "Description": "Caribbean country"}, "cs": {"Name": "Karibské Nizozemsko", "Description": "Karibská země"}, "sk": {"Name": "Karibské Holandsko", "Description": "Karibská krajina"}, "uk": {"Name": "Карибські Нідерланди", "Description": "Карибська країна"}, "ru": {"Name": "Бонэйр, Синт-Эстатиус и Саба", "Description": "Карибская страна"}}', false),
  ('Burkina Faso', 'BFA', 'BF', '{"en": {"Name": "Burkina Faso", "Description": "West African country"}, "cs": {"Name": "Burkina Faso", "Description": "Západoafrická země"}, "sk": {"Name": "Burkina Faso", "Description": "Západoafrická krajina"}, "uk": {"Name": "Буркіна-Фасо", "Description": "Західноафриканська країна"}, "ru": {"Name": "Буркина-Фасо", "Description": "Западноафриканская страна"}}', false),
  ('Bangladesh', 'BGD', 'BD', '{"en": {"Name": "Bangladesh", "Description": "South Asian country"}, "cs": {"Name": "Bangladéš", "Description": "Jihoasijská země"}, "sk": {"Name": "Bangladéš", "Description": "Juhoázijská krajina"}, "uk": {"Name": "Бангладеш", "Description": "Південноазійська країна"}, "ru": {"Name": "Бангладеш", "Description": "Южноазиатская страна"}}', false),
  ('Bulgaria', 'BGR', 'BG', '{"en": {"Name": "Bulgaria", "Description": "Southeastern European country"}, "cs": {"Name": "Bulharsko", "Description": "Jihovýchodní evropská země"}, "sk": {"Name": "Bulharsko", "Description": "Juhovýchodná európska krajina"}, "uk": {"Name": "Болгарія", "Description": "Південно-східна європейська країна"}, "ru": {"Name": "Болгария", "Description": "Юго-восточная европейская страна"}}', false),
  ('Bahrain', 'BHR', 'BH', '{"en": {"Name": "Bahrain", "Description": "Western Asian country"}, "cs": {"Name": "Bahrajn", "Description": "Západoasijská země"}, "sk": {"Name": "Bahrajn", "Description": "Západoázijská krajina"}, "uk": {"Name": "Бахрейн", "Description": "Західноазійська країна"}, "ru": {"Name": "Бахрейн", "Description": "Западноазиатская страна"}}', false),
  ('Bahamas', 'BHS', 'BS', '{"en": {"Name": "Bahamas", "Description": "Caribbean country"}, "cs": {"Name": "Bahamy", "Description": "Karibská země"}, "sk": {"Name": "Bahamy", "Description": "Karibská krajina"}, "uk": {"Name": "Багамські Острови", "Description": "Карибська країна"}, "ru": {"Name": "Багамы", "Description": "Карибская страна"}}', false),
  ('Bosnia & Herzegovina', 'BIH', 'BA', '{"en": {"Name": "Bosnia & Herzegovina", "Description": "Southeastern European country"}, "cs": {"Name": "Bosna a Hercegovina", "Description": "Jihovýchodní evropská země"}, "sk": {"Name": "Bosna a Hercegovina", "Description": "Juhovýchodná európska krajina"}, "uk": {"Name": "Боснія і Герцеговина", "Description": "Південно-східна європейська країна"}, "ru": {"Name": "Босния и Герцеговина", "Description": "Юго-восточная европейская страна"}}', false),
  ('St. Barthélemy', 'BLM', 'BL', '{"en": {"Name": "St. Barthélemy", "Description": "Caribbean country"}, "cs": {"Name": "Svatý Bartoloměj", "Description": "Karibská země"}, "sk": {"Name": "Svätý Bartolomej", "Description": "Karibská krajina"}, "uk": {"Name": "Сен-Бартелемі", "Description": "Карибська країна"}, "ru": {"Name": "Сен-Бартелеми", "Description": "Карибская страна"}}', false),
  ('Belarus', 'BLR', 'BY', '{"en": {"Name": "Belarus", "Description": "Eastern European country"}, "cs": {"Name": "Bělorusko", "Description": "Východoevropská země"}, "sk": {"Name": "Bielorusko", "Description": "Východoeurópska krajina"}, "uk": {"Name": "Білорусь", "Description": "Східноєвропейська країна"}, "ru": {"Name": "Беларусь", "Description": "Восточноевропейская страна"}}', false),
  ('Belize', 'BLZ', 'BZ', '{"en": {"Name": "Belize", "Description": "Central American country"}, "cs": {"Name": "Belize", "Description": "Středoamerická země"}, "sk": {"Name": "Belize", "Description": "Stredoamerická krajina"}, "uk": {"Name": "Беліз", "Description": "Центральноамериканська країна"}, "ru": {"Name": "Белиз", "Description": "Центральноамериканская страна"}}', false),
  ('Bermuda', 'BMU', 'BM', '{"en": {"Name": "Bermuda", "Description": "North American country"}, "cs": {"Name": "Bermudy", "Description": "Severoamerická země"}, "sk": {"Name": "Bermudy", "Description": "Severoamerická krajina"}, "uk": {"Name": "Бермудські Острови", "Description": "Північноамериканська країна"}, "ru": {"Name": "Бермудские о-ва", "Description": "Североамериканская страна"}}', false),
  ('Bolivia', 'BOL', 'BO', '{"en": {"Name": "Bolivia", "Description": "South American country"}, "cs": {"Name": "Bolívie", "Description": "Jihoamerická země"}, "sk": {"Name": "Bolívia", "Description": "Juhoamerická krajina"}, "uk": {"Name": "Болівія", "Description": "Південноамериканська країна"}, "ru": {"Name": "Боливия", "Description": "Южноамериканская страна"}}', false),
  ('Brazil', 'BRA', 'BR', '{"en": {"Name": "Brazil", "Description": "South American country"}, "cs": {"Name": "Brazílie", "Description": "Jihoamerická země"}, "sk": {"Name": "Brazília", "Description": "Juhoamerická krajina"}, "uk": {"Name": "Бразилія", "Description": "Південноамериканська країна"}, "ru": {"Name": "Бразилия", "Description": "Южноамериканская страна"}}', false),
  ('Barbados', 'BRB', 'BB', '{"en": {"Name": "Barbados", "Description": "Caribbean country"}, "cs": {"Name": "Barbados", "Description": "Karibská země"}, "sk": {"Name": "Barbados", "Description": "Karibská krajina"}, "uk": {"Name": "Барбадос", "Description": "Карибська країна"}, "ru": {"Name": "Барбадос", "Description": "Карибская страна"}}', false),
  ('Brunei', 'BRN', 'BN', '{"en": {"Name": "Brunei", "Description": "Southeast Asian country"}, "cs": {"Name": "Brunej", "Description": "Jihovýchodní asijská země"}, "sk": {"Name": "Brunej", "Description": "Juhovýchodná ázijská krajina"}, "uk": {"Name": "Бруней", "Description": "Південно-східна азійська країна"}, "ru": {"Name": "Бруней", "Description": "Юго-восточная азиатская страна"}}', false),
  ('Bhutan', 'BTN', 'BT', '{"en": {"Name": "Bhutan", "Description": "South Asian country"}, "cs": {"Name": "Bhútán", "Description": "Jihoasijská země"}, "sk": {"Name": "Bhután", "Description": "Juhoázijská krajina"}, "uk": {"Name": "Бутан", "Description": "Південноазійська країна"}, "ru": {"Name": "Бутан", "Description": "Южноазиатская страна"}}', false),
  ('Bouvet Island', 'BVT', 'BV', '{"en": {"Name": "Bouvet Island", "Description": "Antarctic territory"}, "cs": {"Name": "Bouvetův ostrov", "Description": "Antarktické území"}, "sk": {"Name": "Bouvetov ostrov", "Description": "Antarktické územie"}, "uk": {"Name": "Острів Буве", "Description": "Антарктична територія"}, "ru": {"Name": "о-в Буве", "Description": "Антарктическая территория"}}', false),
  ('Botswana', 'BWA', 'BW', '{"en": {"Name": "Botswana", "Description": "Southern African country"}, "cs": {"Name": "Botswana", "Description": "Jihoafrická země"}, "sk": {"Name": "Botswana", "Description": "Juhoafrická krajina"}, "uk": {"Name": "Ботсвана", "Description": "Південноафриканська країна"}, "ru": {"Name": "Ботсвана", "Description": "Южноафриканская страна"}}', false),
  ('Central African Republic', 'CAF', 'CF', '{"en": {"Name": "Central African Republic", "Description": "Central African country"}, "cs": {"Name": "Středoafrická republika", "Description": "Středoafrická země"}, "sk": {"Name": "Stredoafrická republika", "Description": "Stredoafrická krajina"}, "uk": {"Name": "Центральноафриканська Республіка", "Description": "Центральноафриканська країна"}, "ru": {"Name": "Центрально-Африканская Республика", "Description": "Центральноафриканская страна"}}', false),
  ('Canada', 'CAN', 'CA', '{"en": {"Name": "Canada", "Description": "North American country"}, "cs": {"Name": "Kanada", "Description": "Severoamerická země"}, "sk": {"Name": "Kanada", "Description": "Severoamerická krajina"}, "uk": {"Name": "Канада", "Description": "Північноамериканська країна"}, "ru": {"Name": "Канада", "Description": "Североамериканская страна"}}', false),
  ('Cocos (Keeling) Islands', 'CCK', 'CC', '{"en": {"Name": "Cocos (Keeling) Islands", "Description": "Oceanic country"}, "cs": {"Name": "Kokosové ostrovy", "Description": "Oceánská země"}, "sk": {"Name": "Kokosové ostrovy", "Description": "Oceánska krajina"}, "uk": {"Name": "Кокосові (Кілінг) Острови", "Description": "Океанійська країна"}, "ru": {"Name": "Кокосовые о-ва", "Description": "Океанская страна"}}', false),
  ('Switzerland', 'CHE', 'CH', '{"en": {"Name": "Switzerland", "Description": "Central European country"}, "cs": {"Name": "Švýcarsko", "Description": "Středoevropská země"}, "sk": {"Name": "Švajčiarsko", "Description": "Stredoeurópska krajina"}, "uk": {"Name": "Швейцарія", "Description": "Центральноєвропейська країна"}, "ru": {"Name": "Швейцария", "Description": "Центральноевропейская страна"}}', false),
  ('Chile', 'CHL', 'CL', '{"en": {"Name": "Chile", "Description": "South American country"}, "cs": {"Name": "Chile", "Description": "Jihoamerická země"}, "sk": {"Name": "Čile", "Description": "Juhoamerická krajina"}, "uk": {"Name": "Чилі", "Description": "Південноамериканська країна"}, "ru": {"Name": "Чили", "Description": "Южноамериканская страна"}}', false),
  ('China', 'CHN', 'CN', '{"en": {"Name": "China", "Description": "East Asian country"}, "cs": {"Name": "Čína", "Description": "Východoasijská země"}, "sk": {"Name": "Čína", "Description": "Východoázijská krajina"}, "uk": {"Name": "Китай", "Description": "Східноазійська країна"}, "ru": {"Name": "Китай", "Description": "Восточноазиатская страна"}}', false),
  ('Côte d’Ivoire', 'CIV', 'CI', '{"en": {"Name": "Côte d’Ivoire", "Description": "West African country"}, "cs": {"Name": "Pobřeží slonoviny", "Description": "Západoafrická země"}, "sk": {"Name": "Pobrežie Slonoviny", "Description": "Západoafrická krajina"}, "uk": {"Name": "Кот-дʼІвуар", "Description": "Західноафриканська країна"}, "ru": {"Name": "Кот-д’Ивуар", "Description": "Западноафриканская страна"}}', false),
  ('Cameroon', 'CMR', 'CM', '{"en": {"Name": "Cameroon", "Description": "Central African country"}, "cs": {"Name": "Kamerun", "Description": "Středoafrická země"}, "sk": {"Name": "Kamerun", "Description": "Stredoafrická krajina"}, "uk": {"Name": "Камерун", "Description": "Центральноафриканська країна"}, "ru": {"Name": "Камерун", "Description": "Центральноафриканская страна"}}', false),
  ('Congo - Kinshasa', 'COD', 'CD', '{"en": {"Name": "Congo - Kinshasa", "Description": "Central African country"}, "cs": {"Name": "Kongo – Kinshasa", "Description": "Středoafrická země"}, "sk": {"Name": "Konžská demokratická republika", "Description": "Stredoafrická krajina"}, "uk": {"Name": "Конго – Кіншаса", "Description": "Центральноафриканська країна"}, "ru": {"Name": "Конго - Киншаса", "Description": "Центральноафриканская страна"}}', false),
  ('Congo - Brazzaville', 'COG', 'CG', '{"en": {"Name": "Congo - Brazzaville", "Description": "Central African country"}, "cs": {"Name": "Kongo – Brazzaville", "Description": "Středoafrická země"}, "sk": {"Name": "Konžská republika", "Description": "Stredoafrická krajina"}, "uk": {"Name": "Конго – Браззавіль", "Description": "Центральноафриканська країна"}, "ru": {"Name": "Конго - Браззавиль", "Description": "Центральноафриканская страна"}}', false),
  ('Cook Islands', 'COK', 'CK', '{"en": {"Name": "Cook Islands", "Description": "Oceanic country"}, "cs": {"Name": "Cookovy ostrovy", "Description": "Oceánská země"}, "sk": {"Name": "Cookove ostrovy", "Description": "Oceánska krajina"}, "uk": {"Name": "Острови Кука", "Description": "Океанійська країна"}, "ru": {"Name": "о-ва Кука", "Description": "Океанская страна"}}', false),
  ('Colombia', 'COL', 'CO', '{"en": {"Name": "Colombia", "Description": "South American country"}, "cs": {"Name": "Kolumbie", "Description": "Jihoamerická země"}, "sk": {"Name": "Kolumbia", "Description": "Juhoamerická krajina"}, "uk": {"Name": "Колумбія", "Description": "Південноамериканська країна"}, "ru": {"Name": "Колумбия", "Description": "Южноамериканская страна"}}', false),
  ('Comoros', 'COM', 'KM', '{"en": {"Name": "Comoros", "Description": "East African country"}, "cs": {"Name": "Komory", "Description": "Východoafrická země"}, "sk": {"Name": "Komory", "Description": "Východoafrická krajina"}, "uk": {"Name": "Комори", "Description": "Східноафриканська країна"}, "ru": {"Name": "Коморы", "Description": "Восточноафриканская страна"}}', false),
  ('Cape Verde', 'CPV', 'CV', '{"en": {"Name": "Cape Verde", "Description": "West African country"}, "cs": {"Name": "Kapverdy", "Description": "Západoafrická země"}, "sk": {"Name": "Kapverdy", "Description": "Západoafrická krajina"}, "uk": {"Name": "Кабо-Верде", "Description": "Західноафриканська країна"}, "ru": {"Name": "Кабо-Верде", "Description": "Западноафриканская страна"}}', false),
  ('Costa Rica', 'CRI', 'CR', '{"en": {"Name": "Costa Rica", "Description": "Central American country"}, "cs": {"Name": "Kostarika", "Description": "Středoamerická země"}, "sk": {"Name": "Kostarika", "Description": "Stredoamerická krajina"}, "uk": {"Name": "Коста-Рика", "Description": "Центральноамериканська країна"}, "ru": {"Name": "Коста-Рика", "Description": "Центральноамериканская страна"}}', false),
  ('Cuba', 'CUB', 'CU', '{"en": {"Name": "Cuba", "Description": "Caribbean country"}, "cs": {"Name": "Kuba", "Description": "Karibská země"}, "sk": {"Name": "Kuba", "Description": "Karibská krajina"}, "uk": {"Name": "Куба", "Description": "Карибська країна"}, "ru": {"Name": "Куба", "Description": "Карибская страна"}}', false),
  ('Curaçao', 'CUW', 'CW', '{"en": {"Name": "Curaçao", "Description": "Caribbean country"}, "cs": {"Name": "Curaçao", "Description": "Karibská země"}, "sk": {"Name": "Curaçao", "Description": "Karibská krajina"}, "uk": {"Name": "Кюрасао", "Description": "Карибська країна"}, "ru": {"Name": "Кюрасао", "Description": "Карибская страна"}}', false),
  ('Christmas Island', 'CXR', 'CX', '{"en": {"Name": "Christmas Island", "Description": "Oceanic country"}, "cs": {"Name": "Vánoční ostrov", "Description": "Oceánská země"}, "sk": {"Name": "Vianočný ostrov", "Description": "Oceánska krajina"}, "uk": {"Name": "Острів Різдва", "Description": "Океанійська країна"}, "ru": {"Name": "о-в Рождества", "Description": "Океанская страна"}}', false),
  ('Cayman Islands', 'CYM', 'KY', '{"en": {"Name": "Cayman Islands", "Description": "Caribbean country"}, "cs": {"Name": "Kajmanské ostrovy", "Description": "Karibská země"}, "sk": {"Name": "Kajmanie ostrovy", "Description": "Karibská krajina"}, "uk": {"Name": "Кайманові Острови", "Description": "Карибська країна"}, "ru": {"Name": "о-ва Кайман", "Description": "Карибская страна"}}', false),
  ('Cyprus', 'CYP', 'CY', '{"en": {"Name": "Cyprus", "Description": "Southeastern European country"}, "cs": {"Name": "Kypr", "Description": "Jihovýchodní evropská země"}, "sk": {"Name": "Cyprus", "Description": "Juhovýchodná európska krajina"}, "uk": {"Name": "Кіпр", "Description": "Південно-східна європейська країна"}, "ru": {"Name": "Кипр", "Description": "Юго-восточная европейская страна"}}', false),
  ('Czech Republic', 'CZE', 'CZ', '{"en": {"Name": "Czech Republic", "Description": "Central European country"}, "cs": {"Name": "Česká republika", "Description": "Středoevropská země"}, "sk": {"Name": "Česko", "Description": "Stredoeurópska krajina"}, "uk": {"Name": "Чехія", "Description": "Центральноєвропейська країна"}, "ru": {"Name": "Чешская Республика", "Description": "Центральноевропейская страна"}}', true),
  ('Germany', 'DEU', 'DE', '{"en": {"Name": "Germany", "Description": "Central European country"}, "cs": {"Name": "Německo", "Description": "Středoevropská země"}, "sk": {"Name": "Nemecko", "Description": "Stredoeurópska krajina"}, "uk": {"Name": "Німеччина", "Description": "Центральноєвропейська країна"}, "ru": {"Name": "Германия", "Description": "Центральноевропейская страна"}}', false),
  ('Djibouti', 'DJI', 'DJ', '{"en": {"Name": "Djibouti", "Description": "East African country"}, "cs": {"Name": "Džibutsko", "Description": "Východoafrická země"}, "sk": {"Name": "Džibutsko", "Description": "Východoafrická krajina"}, "uk": {"Name": "Джибуті", "Description": "Східноафриканська країна"}, "ru": {"Name": "Джибути", "Description": "Восточноафриканская страна"}}', false),
  ('Dominica', 'DMA', 'DM', '{"en": {"Name": "Dominica", "Description": "Caribbean country"}, "cs": {"Name": "Dominika", "Description": "Karibská země"}, "sk": {"Name": "Dominika", "Description": "Karibská krajina"}, "uk": {"Name": "Домініка", "Description": "Карибська країна"}, "ru": {"Name": "Доминика", "Description": "Карибская страна"}}', false),
  ('Denmark', 'DNK', 'DK', '{"en": {"Name": "Denmark", "Description": "Northern European country"}, "cs": {"Name": "Dánsko", "Description": "Severní evropská země"}, "sk": {"Name": "Dánsko", "Description": "Severná európska krajina"}, "uk": {"Name": "Данія", "Description": "Північноєвропейська країна"}, "ru": {"Name": "Дания", "Description": "Северная европейская страна"}}', false),
  ('Dominican Republic', 'DOM', 'DO', '{"en": {"Name": "Dominican Republic", "Description": "Caribbean country"}, "cs": {"Name": "Dominikánská republika", "Description": "Karibská země"}, "sk": {"Name": "Dominikánska republika", "Description": "Karibská krajina"}, "uk": {"Name": "Домініканська Республіка", "Description": "Карибська країна"}, "ru": {"Name": "Доминиканская Республика", "Description": "Карибская страна"}}', false),
  ('Algeria', 'DZA', 'DZ', '{"en": {"Name": "Algeria", "Description": "North African country"}, "cs": {"Name": "Alžírsko", "Description": "Severoafrická země"}, "sk": {"Name": "Alžírsko", "Description": "Severoafrická krajina"}, "uk": {"Name": "Алжир", "Description": "Північноафриканська країна"}, "ru": {"Name": "Алжир", "Description": "Североафриканская страна"}}', false),
  ('Ecuador', 'ECU', 'EC', '{"en": {"Name": "Ecuador", "Description": "South American country"}, "cs": {"Name": "Ekvádor", "Description": "Jihoamerická země"}, "sk": {"Name": "Ekvádor", "Description": "Juhoamerická krajina"}, "uk": {"Name": "Еквадор", "Description": "Південноамериканська країна"}, "ru": {"Name": "Эквадор", "Description": "Южноамериканская страна"}}', false),
  ('Egypt', 'EGY', 'EG', '{"en": {"Name": "Egypt", "Description": "North African country"}, "cs": {"Name": "Egypt", "Description": "Severoafrická země"}, "sk": {"Name": "Egypt", "Description": "Severoafrická krajina"}, "uk": {"Name": "Єгипет", "Description": "Північноафриканська країна"}, "ru": {"Name": "Египет", "Description": "Североафриканская страна"}}', false),
  ('Eritrea', 'ERI', 'ER', '{"en": {"Name": "Eritrea", "Description": "East African country"}, "cs": {"Name": "Eritrea", "Description": "Východoafrická země"}, "sk": {"Name": "Eritrea", "Description": "Východoafrická krajina"}, "uk": {"Name": "Еритрея", "Description": "Східноафриканська країна"}, "ru": {"Name": "Эритрея", "Description": "Восточноафриканская страна"}}', false),
  ('Western Sahara', 'ESH', 'EH', '{"en": {"Name": "Western Sahara", "Description": "North African country"}, "cs": {"Name": "Západní Sahara", "Description": "Severoafrická země"}, "sk": {"Name": "Západná Sahara", "Description": "Severoafrická krajina"}, "uk": {"Name": "Західна Сахара", "Description": "Північноафриканська країна"}, "ru": {"Name": "Западная Сахара", "Description": "Североафриканская страна"}}', false),
  ('Spain', 'ESP', 'ES', '{"en": {"Name": "Spain", "Description": "Southwestern European country"}, "cs": {"Name": "Španělsko", "Description": "Jihozápadní evropská země"}, "sk": {"Name": "Španielsko", "Description": "Juhozápadná európska krajina"}, "uk": {"Name": "Іспанія", "Description": "Південно-західна європейська країна"}, "ru": {"Name": "Испания", "Description": "Юго-западная европейская страна"}}', false),
  ('Estonia', 'EST', 'EE', '{"en": {"Name": "Estonia", "Description": "Northern European country"}, "cs": {"Name": "Estonsko", "Description": "Severní evropská země"}, "sk": {"Name": "Estónsko", "Description": "Severná európska krajina"}, "uk": {"Name": "Естонія", "Description": "Північноєвропейська країна"}, "ru": {"Name": "Эстония", "Description": "Северная европейская страна"}}', false),
  ('Ethiopia', 'ETH', 'ET', '{"en": {"Name": "Ethiopia", "Description": "East African country"}, "cs": {"Name": "Etiopie", "Description": "Východoafrická země"}, "sk": {"Name": "Etiópia", "Description": "Východoafrická krajina"}, "uk": {"Name": "Ефіопія", "Description": "Східноафриканська країна"}, "ru": {"Name": "Эфиопия", "Description": "Восточноафриканская страна"}}', false),
  ('Finland', 'FIN', 'FI', '{"en": {"Name": "Finland", "Description": "Northern European country"}, "cs": {"Name": "Finsko", "Description": "Severní evropská země"}, "sk": {"Name": "Fínsko", "Description": "Severná európska krajina"}, "uk": {"Name": "Фінляндія", "Description": "Північноєвропейська країна"}, "ru": {"Name": "Финляндия", "Description": "Северная европейская страна"}}', false),
  ('Fiji', 'FJI', 'FJ', '{"en": {"Name": "Fiji", "Description": "Oceanic country"}, "cs": {"Name": "Fidži", "Description": "Oceánská země"}, "sk": {"Name": "Fidži", "Description": "Oceánska krajina"}, "uk": {"Name": "Фіджі", "Description": "Океанійська країна"}, "ru": {"Name": "Фиджи", "Description": "Океанская страна"}}', false),
  ('Falkland Islands', 'FLK', 'FK', '{"en": {"Name": "Falkland Islands", "Description": "South American country"}, "cs": {"Name": "Falklandské ostrovy", "Description": "Jihoamerická země"}, "sk": {"Name": "Falklandy", "Description": "Juhoamerická krajina"}, "uk": {"Name": "Фолклендські Острови", "Description": "Південноамериканська країна"}, "ru": {"Name": "Фолклендские о-ва", "Description": "Южноамериканская страна"}}', false),
  ('France', 'FRA', 'FR', '{"en": {"Name": "France", "Description": "Western European country"}, "cs": {"Name": "Francie", "Description": "Západoevropská země"}, "sk": {"Name": "Francúzsko", "Description": "Západoeurópska krajina"}, "uk": {"Name": "Франція", "Description": "Західноєвропейська країна"}, "ru": {"Name": "Франция", "Description": "Западноевропейская страна"}}', false),
  ('Faroe Islands', 'FRO', 'FO', '{"en": {"Name": "Faroe Islands", "Description": "Northern European country"}, "cs": {"Name": "Faerské ostrovy", "Description": "Severní evropská země"}, "sk": {"Name": "Faerské ostrovy", "Description": "Severná európska krajina"}, "uk": {"Name": "Фарерські Острови", "Description": "Північноєвропейська країна"}, "ru": {"Name": "Фарерские о-ва", "Description": "Северная европейская страна"}}', false),
  ('Micronesia', 'FSM', 'FM', '{"en": {"Name": "Micronesia", "Description": "Oceanic country"}, "cs": {"Name": "Mikronésie", "Description": "Oceánská země"}, "sk": {"Name": "Mikronézia", "Description": "Oceánska krajina"}, "uk": {"Name": "Мікронезія", "Description": "Океанійська країна"}, "ru": {"Name": "Федеративные Штаты Микронезии", "Description": "Океанская страна"}}', false),
  ('Gabon', 'GAB', 'GA', '{"en": {"Name": "Gabon", "Description": "Central African country"}, "cs": {"Name": "Gabon", "Description": "Středoafrická země"}, "sk": {"Name": "Gabon", "Description": "Stredoafrická krajina"}, "uk": {"Name": "Габон", "Description": "Центральноафриканська країна"}, "ru": {"Name": "Габон", "Description": "Центральноафриканская страна"}}', false),
  ('United Kingdom', 'GBR', 'GB', '{"en": {"Name": "United Kingdom", "Description": "Northwestern European country"}, "cs": {"Name": "Velká Británie", "Description": "Severozápadní evropská země"}, "sk": {"Name": "Spojené kráľovstvo", "Description": "Severozápadná európska krajina"}, "uk": {"Name": "Велика Британія", "Description": "Північно-західна європейська країна"}, "ru": {"Name": "Великобритания", "Description": "Северо-западная европейская страна"}}', false),
  ('Georgia', 'GEO', 'GE', '{"en": {"Name": "Georgia", "Description": "Western Asian country"}, "cs": {"Name": "Gruzie", "Description": "Západoasijská země"}, "sk": {"Name": "Gruzínsko", "Description": "Západoázijská krajina"}, "uk": {"Name": "Грузія", "Description": "Західноазійська країна"}, "ru": {"Name": "Грузия", "Description": "Западноазиатская страна"}}', false),
  ('Guernsey', 'GGY', 'GG', '{"en": {"Name": "Guernsey", "Description": "Northern European country"}, "cs": {"Name": "Guernsey", "Description": "Severní evropská země"}, "sk": {"Name": "Guernsey", "Description": "Severná európska krajina"}, "uk": {"Name": "Гернсі", "Description": "Північноєвропейська країна"}, "ru": {"Name": "Гернси", "Description": "Северная европейская страна"}}', false),
  ('Ghana', 'GHA', 'GH', '{"en": {"Name": "Ghana", "Description": "West African country"}, "cs": {"Name": "Ghana", "Description": "Západoafrická země"}, "sk": {"Name": "Ghana", "Description": "Západoafrická krajina"}, "uk": {"Name": "Гана", "Description": "Західноафриканська країна"}, "ru": {"Name": "Гана", "Description": "Западноафриканская страна"}}', false),
  ('Gibraltar', 'GIB', 'GI', '{"en": {"Name": "Gibraltar", "Description": "Southern European country"}, "cs": {"Name": "Gibraltar", "Description": "Jihoevropská země"}, "sk": {"Name": "Gibraltár", "Description": "Juhoeurópska krajina"}, "uk": {"Name": "Гібралтар", "Description": "Південноєвропейська країна"}, "ru": {"Name": "Гибралтар", "Description": "Южноевропейская страна"}}', false),
  ('Guinea', 'GIN', 'GN', '{"en": {"Name": "Guinea", "Description": "West African country"}, "cs": {"Name": "Guinea", "Description": "Západoafrická země"}, "sk": {"Name": "Guinea", "Description": "Západoafrická krajina"}, "uk": {"Name": "Гвінея", "Description": "Західноафриканська країна"}, "ru": {"Name": "Гвинея", "Description": "Западноафриканская страна"}}', false),
  ('Guadeloupe', 'GLP', 'GP', '{"en": {"Name": "Guadeloupe", "Description": "Caribbean country"}, "cs": {"Name": "Guadeloupe", "Description": "Karibská země"}, "sk": {"Name": "Guadeloupe", "Description": "Karibská krajina"}, "uk": {"Name": "Гваделупа", "Description": "Карибська країна"}, "ru": {"Name": "Гваделупа", "Description": "Карибская страна"}}', false),
  ('Gambia', 'GMB', 'GM', '{"en": {"Name": "Gambia", "Description": "West African country"}, "cs": {"Name": "Gambie", "Description": "Západoafrická země"}, "sk": {"Name": "Gambia", "Description": "Západoafrická krajina"}, "uk": {"Name": "Гамбія", "Description": "Західноафриканська країна"}, "ru": {"Name": "Гамбия", "Description": "Западноафриканская страна"}}', false),
  ('Guinea-Bissau', 'GNB', 'GW', '{"en": {"Name": "Guinea-Bissau", "Description": "West African country"}, "cs": {"Name": "Guinea-Bissau", "Description": "Západoafrická země"}, "sk": {"Name": "Guinea-Bissau", "Description": "Západoafrická krajina"}, "uk": {"Name": "Гвінея-Бісау", "Description": "Західноафриканська країна"}, "ru": {"Name": "Гвинея-Бисау", "Description": "Западноафриканская страна"}}', false),
  ('Equatorial Guinea', 'GNQ', 'GQ', '{"en": {"Name": "Equatorial Guinea", "Description": "Central African country"}, "cs": {"Name": "Rovníková Guinea", "Description": "Středoafrická země"}, "sk": {"Name": "Rovníková Guinea", "Description": "Stredoafrická krajina"}, "uk": {"Name": "Екваторіальна Гвінея", "Description": "Центральноафриканська країна"}, "ru": {"Name": "Экваториальная Гвинея", "Description": "Центральноафриканская страна"}}', false),
  ('Greece', 'GRC', 'GR', '{"en": {"Name": "Greece", "Description": "Southeastern European country"}, "cs": {"Name": "Řecko", "Description": "Jihovýchodní evropská země"}, "sk": {"Name": "Grécko", "Description": "Juhovýchodná európska krajina"}, "uk": {"Name": "Греція", "Description": "Південно-східна європейська країна"}, "ru": {"Name": "Греция", "Description": "Юго-восточная европейская страна"}}', false),
  ('Grenada', 'GRD', 'GD', '{"en": {"Name": "Grenada", "Description": "Caribbean country"}, "cs": {"Name": "Grenada", "Description": "Karibská země"}, "sk": {"Name": "Grenada", "Description": "Karibská krajina"}, "uk": {"Name": "Гренада", "Description": "Карибська країна"}, "ru": {"Name": "Гренада", "Description": "Карибская страна"}}', false),
  ('Greenland', 'GRL', 'GL', '{"en": {"Name": "Greenland", "Description": "North American country"}, "cs": {"Name": "Grónsko", "Description": "Severoamerická země"}, "sk": {"Name": "Grónsko", "Description": "Severoamerická krajina"}, "uk": {"Name": "Гренландія", "Description": "Північноамериканська країна"}, "ru": {"Name": "Гренландия", "Description": "Североамериканская страна"}}', false),
  ('Guatemala', 'GTM', 'GT', '{"en": {"Name": "Guatemala", "Description": "Central American country"}, "cs": {"Name": "Guatemala", "Description": "Středoamerická země"}, "sk": {"Name": "Guatemala", "Description": "Stredoamerická krajina"}, "uk": {"Name": "Гватемала", "Description": "Центральноамериканська країна"}, "ru": {"Name": "Гватемала", "Description": "Центральноамериканская страна"}}', false),
  ('French Guiana', 'GUF', 'GF', '{"en": {"Name": "French Guiana", "Description": "South American country"}, "cs": {"Name": "Francouzská Guyana", "Description": "Jihoamerická země"}, "sk": {"Name": "Francúzska Guyana", "Description": "Juhoamerická krajina"}, "uk": {"Name": "Французька Гвіана", "Description": "Південноамериканська країна"}, "ru": {"Name": "Французская Гвиана", "Description": "Южноамериканская страна"}}', false),
  ('Guam', 'GUM', 'GU', '{"en": {"Name": "Guam", "Description": "Oceanic country"}, "cs": {"Name": "Guam", "Description": "Oceánská země"}, "sk": {"Name": "Guam", "Description": "Oceánska krajina"}, "uk": {"Name": "Гуам", "Description": "Океанійська країна"}, "ru": {"Name": "Гуам", "Description": "Океанская страна"}}', false),
  ('Guyana', 'GUY', 'GY', '{"en": {"Name": "Guyana", "Description": "South American country"}, "cs": {"Name": "Guyana", "Description": "Jihoamerická země"}, "sk": {"Name": "Guyana", "Description": "Juhoamerická krajina"}, "uk": {"Name": "Гаяна", "Description": "Південноамериканська країна"}, "ru": {"Name": "Гайана", "Description": "Южноамериканская страна"}}', false),
  ('Hong Kong SAR China', 'HKG', 'HK', '{"en": {"Name": "Hong Kong SAR China", "Description": "East Asian country"}, "cs": {"Name": "Hongkong – ZAO Číny", "Description": "Východoasijská země"}, "sk": {"Name": "Hongkong – OAO Číny", "Description": "Východoázijská krajina"}, "uk": {"Name": "Гонконг, ОАР Китаю", "Description": "Східноазійська країна"}, "ru": {"Name": "Гонконг (САР)", "Description": "Восточноазиатская страна"}}', false),
  ('Heard & McDonald Islands', 'HMD', 'HM', '{"en": {"Name": "Heard & McDonald Islands", "Description": "Antarctic territory"}, "cs": {"Name": "Heardův ostrov a McDonaldovy ostrovy", "Description": "Antarktické území"}, "sk": {"Name": "Heardov ostrov a Macdonaldove ostrovy", "Description": "Antarktické územie"}, "uk": {"Name": "Острови Герд і Макдоналд", "Description": "Антарктична територія"}, "ru": {"Name": "о-ва Херд и Макдональд", "Description": "Антарктическая территория"}}', false),
  ('Honduras', 'HND', 'HN', '{"en": {"Name": "Honduras", "Description": "Central American country"}, "cs": {"Name": "Honduras", "Description": "Středoamerická země"}, "sk": {"Name": "Honduras", "Description": "Stredoamerická krajina"}, "uk": {"Name": "Гондурас", "Description": "Центральноамериканська країна"}, "ru": {"Name": "Гондурас", "Description": "Центральноамериканская страна"}}', false),
  ('Croatia', 'HRV', 'HR', '{"en": {"Name": "Croatia", "Description": "Southeastern European country"}, "cs": {"Name": "Chorvatsko", "Description": "Jihovýchodní evropská země"}, "sk": {"Name": "Chorvátsko", "Description": "Juhovýchodná európska krajina"}, "uk": {"Name": "Хорватія", "Description": "Південно-східна європейська країна"}, "ru": {"Name": "Хорватия", "Description": "Юго-восточная европейская страна"}}', false),
  ('Haiti', 'HTI', 'HT', '{"en": {"Name": "Haiti", "Description": "Caribbean country"}, "cs": {"Name": "Haiti", "Description": "Karibská země"}, "sk": {"Name": "Haiti", "Description": "Karibská krajina"}, "uk": {"Name": "Гаїті", "Description": "Карибська країна"}, "ru": {"Name": "Гаити", "Description": "Карибская страна"}}', false),
  ('Hungary', 'HUN', 'HU', '{"en": {"Name": "Hungary", "Description": "Central European country"}, "cs": {"Name": "Maďarsko", "Description": "Středoevropská země"}, "sk": {"Name": "Maďarsko", "Description": "Stredoeurópska krajina"}, "uk": {"Name": "Угорщина", "Description": "Центральноєвропейська країна"}, "ru": {"Name": "Венгрия", "Description": "Центральноевропейская страна"}}', false),
  ('Indonesia', 'IDN', 'ID', '{"en": {"Name": "Indonesia", "Description": "Southeast Asian country"}, "cs": {"Name": "Indonésie", "Description": "Jihovýchodní asijská země"}, "sk": {"Name": "Indonézia", "Description": "Juhovýchodná ázijská krajina"}, "uk": {"Name": "Індонезія", "Description": "Південно-східна азійська країна"}, "ru": {"Name": "Индонезия", "Description": "Юго-восточная азиатская страна"}}', false),
  ('Isle of Man', 'IMN', 'IM', '{"en": {"Name": "Isle of Man", "Description": "Northern European country"}, "cs": {"Name": "Ostrov Man", "Description": "Severní evropská země"}, "sk": {"Name": "Ostrov Man", "Description": "Severná európska krajina"}, "uk": {"Name": "Острів Мен", "Description": "Північноєвропейська країна"}, "ru": {"Name": "о-в Мэн", "Description": "Северная европейская страна"}}', false),
  ('India', 'IND', 'IN', '{"en": {"Name": "India", "Description": "South Asian country"}, "cs": {"Name": "Indie", "Description": "Jihoasijská země"}, "sk": {"Name": "India", "Description": "Juhoázijská krajina"}, "uk": {"Name": "Індія", "Description": "Південноазійська країна"}, "ru": {"Name": "Индия", "Description": "Южноазиатская страна"}}', false),
  ('British Indian Ocean Territory', 'IOT', 'IO', '{"en": {"Name": "British Indian Ocean Territory", "Description": "East African country"}, "cs": {"Name": "Britské indickooceánské území", "Description": "Východoafrická země"}, "sk": {"Name": "Britské indickooceánske územie", "Description": "Východoafrická krajina"}, "uk": {"Name": "Британська територія в Індійському океані", "Description": "Східноафриканська країна"}, "ru": {"Name": "Британская территория в Индийском океане", "Description": "Восточноафриканская страна"}}', false),
  ('Ireland', 'IRL', 'IE', '{"en": {"Name": "Ireland", "Description": "Northwestern European country"}, "cs": {"Name": "Irsko", "Description": "Severozápadní evropská země"}, "sk": {"Name": "Írsko", "Description": "Severozápadná európska krajina"}, "uk": {"Name": "Ірландія", "Description": "Північно-західна європейська країна"}, "ru": {"Name": "Ирландия", "Description": "Северо-западная европейская страна"}}', false),
  ('Iran', 'IRN', 'IR', '{"en": {"Name": "Iran", "Description": "South Asian country"}, "cs": {"Name": "Írán", "Description": "Jihoasijská země"}, "sk": {"Name": "Irán", "Description": "Juhoázijská krajina"}, "uk": {"Name": "Іран", "Description": "Південноазійська країна"}, "ru": {"Name": "Иран", "Description": "Южноазиатская страна"}}', false),
  ('Iraq', 'IRQ', 'IQ', '{"en": {"Name": "Iraq", "Description": "Western Asian country"}, "cs": {"Name": "Irák", "Description": "Západoasijská země"}, "sk": {"Name": "Irak", "Description": "Západoázijská krajina"}, "uk": {"Name": "Ірак", "Description": "Західноазійська країна"}, "ru": {"Name": "Ирак", "Description": "Западноазиатская страна"}}', false),
  ('Iceland', 'ISL', 'IS', '{"en": {"Name": "Iceland", "Description": "Northern European country"}, "cs": {"Name": "Island", "Description": "Severní evropská země"}, "sk": {"Name": "Island", "Description": "Severná európska krajina"}, "uk": {"Name": "Ісландія", "Description": "Північноєвропейська країна"}, "ru": {"Name": "Исландия", "Description": "Северная европейская страна"}}', false),
  ('Israel', 'ISR', 'IL', '{"en": {"Name": "Israel", "Description": "Western Asian country"}, "cs": {"Name": "Izrael", "Description": "Západoasijská země"}, "sk": {"Name": "Izrael", "Description": "Západoázijská krajina"}, "uk": {"Name": "Ізраїль", "Description": "Західноазійська країна"}, "ru": {"Name": "Израиль", "Description": "Западноазиатская страна"}}', false),
  ('Italy', 'ITA', 'IT', '{"en": {"Name": "Italy", "Description": "Southern European country"}, "cs": {"Name": "Itálie", "Description": "Jihoevropská země"}, "sk": {"Name": "Taliansko", "Description": "Juhoeurópska krajina"}, "uk": {"Name": "Італія", "Description": "Південноєвропейська країна"}, "ru": {"Name": "Италия", "Description": "Южноевропейская страна"}}', false),
  ('Jamaica', 'JAM', 'JM', '{"en": {"Name": "Jamaica", "Description": "Caribbean country"}, "cs": {"Name": "Jamajka", "Description": "Karibská země"}, "sk": {"Name": "Jamajka", "Description": "Karibská krajina"}, "uk": {"Name": "Ямайка", "Description": "Карибська країна"}, "ru": {"Name": "Ямайка", "Description": "Карибская страна"}}', false),
  ('Jersey', 'JEY', 'JE', '{"en": {"Name": "Jersey", "Description": "Northern European country"}, "cs": {"Name": "Jersey", "Description": "Severní evropská země"}, "sk": {"Name": "Jersey", "Description": "Severná európska krajina"}, "uk": {"Name": "Джерсі", "Description": "Північноєвропейська країна"}, "ru": {"Name": "Джерси", "Description": "Северная европейская страна"}}', false),
  ('Jordan', 'JOR', 'JO', '{"en": {"Name": "Jordan", "Description": "Western Asian country"}, "cs": {"Name": "Jordánsko", "Description": "Západoasijská země"}, "sk": {"Name": "Jordánsko", "Description": "Západoázijská krajina"}, "uk": {"Name": "Йорданія", "Description": "Західноазійська країна"}, "ru": {"Name": "Иордания", "Description": "Западноазиатская страна"}}', false),
  ('Japan', 'JPN', 'JP', '{"en": {"Name": "Japan", "Description": "East Asian country"}, "cs": {"Name": "Japonsko", "Description": "Východoasijská země"}, "sk": {"Name": "Japonsko", "Description": "Východoázijská krajina"}, "uk": {"Name": "Японія", "Description": "Східноазійська країна"}, "ru": {"Name": "Япония", "Description": "Восточноазиатская страна"}}', false),
  ('Kazakhstan', 'KAZ', 'KZ', '{"en": {"Name": "Kazakhstan", "Description": "Central Asian country"}, "cs": {"Name": "Kazachstán", "Description": "Středoasijská země"}, "sk": {"Name": "Kazachstan", "Description": "Stredoázijská krajina"}, "uk": {"Name": "Казахстан", "Description": "Центральноазійська країна"}, "ru": {"Name": "Казахстан", "Description": "Центральноазиатская страна"}}', false),
  ('Kenya', 'KEN', 'KE', '{"en": {"Name": "Kenya", "Description": "East African country"}, "cs": {"Name": "Keňa", "Description": "Východoafrická země"}, "sk": {"Name": "Keňa", "Description": "Východoafrická krajina"}, "uk": {"Name": "Кенія", "Description": "Східноафриканська країна"}, "ru": {"Name": "Кения", "Description": "Восточноафриканская страна"}}', false),
  ('Kyrgyzstan', 'KGZ', 'KG', '{"en": {"Name": "Kyrgyzstan", "Description": "Central Asian country"}, "cs": {"Name": "Kyrgyzstán", "Description": "Středoasijská země"}, "sk": {"Name": "Kirgizsko", "Description": "Stredoázijská krajina"}, "uk": {"Name": "Киргизстан", "Description": "Центральноазійська країна"}, "ru": {"Name": "Киргизия", "Description": "Центральноазиатская страна"}}', false),
  ('Cambodia', 'KHM', 'KH', '{"en": {"Name": "Cambodia", "Description": "Southeast Asian country"}, "cs": {"Name": "Kambodža", "Description": "Jihovýchodní asijská země"}, "sk": {"Name": "Kambodža", "Description": "Juhovýchodná ázijská krajina"}, "uk": {"Name": "Камбоджа", "Description": "Південно-східна азійська країна"}, "ru": {"Name": "Камбоджа", "Description": "Юго-восточная азиатская страна"}}', false),
  ('Kiribati', 'KIR', 'KI', '{"en": {"Name": "Kiribati", "Description": "Oceanic country"}, "cs": {"Name": "Kiribati", "Description": "Oceánská země"}, "sk": {"Name": "Kiribati", "Description": "Oceánska krajina"}, "uk": {"Name": "Кірибаті", "Description": "Океанійська країна"}, "ru": {"Name": "Кирибати", "Description": "Океанская страна"}}', false),
  ('St. Kitts & Nevis', 'KNA', 'KN', '{"en": {"Name": "St. Kitts & Nevis", "Description": "Caribbean country"}, "cs": {"Name": "Svatý Kryštof a Nevis", "Description": "Karibská země"}, "sk": {"Name": "Svätý Krištof a Nevis", "Description": "Karibská krajina"}, "uk": {"Name": "Сент-Кітс і Невіс", "Description": "Карибська країна"}, "ru": {"Name": "Сент-Китс и Невис", "Description": "Карибская страна"}}', false),
  ('South Korea', 'KOR', 'KR', '{"en": {"Name": "South Korea", "Description": "East Asian country"}, "cs": {"Name": "Jižní Korea", "Description": "Východoasijská země"}, "sk": {"Name": "Južná Kórea", "Description": "Východoázijská krajina"}, "uk": {"Name": "Південна Корея", "Description": "Східноазійська країна"}, "ru": {"Name": "Южная Корея", "Description": "Восточноазиатская страна"}}', false),
  ('Kuwait', 'KWT', 'KW', '{"en": {"Name": "Kuwait", "Description": "Western Asian country"}, "cs": {"Name": "Kuvajt", "Description": "Západoasijská země"}, "sk": {"Name": "Kuvajt", "Description": "Západoázijská krajina"}, "uk": {"Name": "Кувейт", "Description": "Західноазійська країна"}, "ru": {"Name": "Кувейт", "Description": "Западноазиатская страна"}}', false),
  ('Laos', 'LAO', 'LA', '{"en": {"Name": "Laos", "Description": "Southeast Asian country"}, "cs": {"Name": "Laos", "Description": "Jihovýchodní asijská země"}, "sk": {"Name": "Laos", "Description": "Juhovýchodná ázijská krajina"}, "uk": {"Name": "Лаос", "Description": "Південно-східна азійська країна"}, "ru": {"Name": "Лаос", "Description": "Юго-восточная азиатская страна"}}', false),
  ('Lebanon', 'LBN', 'LB', '{"en": {"Name": "Lebanon", "Description": "Western Asian country"}, "cs": {"Name": "Libanon", "Description": "Západoasijská země"}, "sk": {"Name": "Libanon", "Description": "Západoázijská krajina"}, "uk": {"Name": "Ліван", "Description": "Західноазійська країна"}, "ru": {"Name": "Ливан", "Description": "Западноазиатская страна"}}', false),
  ('Liberia', 'LBR', 'LR', '{"en": {"Name": "Liberia", "Description": "West African country"}, "cs": {"Name": "Libérie", "Description": "Západoafrická země"}, "sk": {"Name": "Libéria", "Description": "Západoafrická krajina"}, "uk": {"Name": "Ліберія", "Description": "Західноафриканська країна"}, "ru": {"Name": "Либерия", "Description": "Западноафриканская страна"}}', false),
  ('Libya', 'LBY', 'LY', '{"en": {"Name": "Libya", "Description": "North African country"}, "cs": {"Name": "Libye", "Description": "Severoafrická země"}, "sk": {"Name": "Líbya", "Description": "Severoafrická krajina"}, "uk": {"Name": "Лівія", "Description": "Північноафриканська країна"}, "ru": {"Name": "Ливия", "Description": "Североафриканская страна"}}', false),
  ('St. Lucia', 'LCA', 'LC', '{"en": {"Name": "St. Lucia", "Description": "Caribbean country"}, "cs": {"Name": "Svatá Lucie", "Description": "Karibská země"}, "sk": {"Name": "Svätá Lucia", "Description": "Karibská krajina"}, "uk": {"Name": "Сент-Люсія", "Description": "Карибська країна"}, "ru": {"Name": "Сент-Люсия", "Description": "Карибская страна"}}', false),
  ('Liechtenstein', 'LIE', 'LI', '{"en": {"Name": "Liechtenstein", "Description": "Western European country"}, "cs": {"Name": "Lichtenštejnsko", "Description": "Západoevropská země"}, "sk": {"Name": "Lichtenštajnsko", "Description": "Západoeurópska krajina"}, "uk": {"Name": "Ліхтенштейн", "Description": "Західноєвропейська країна"}, "ru": {"Name": "Лихтенштейн", "Description": "Западноевропейская страна"}}', false),
  ('Sri Lanka', 'LKA', 'LK', '{"en": {"Name": "Sri Lanka", "Description": "South Asian country"}, "cs": {"Name": "Srí Lanka", "Description": "Jihoasijská země"}, "sk": {"Name": "Srí Lanka", "Description": "Juhoázijská krajina"}, "uk": {"Name": "Шрі-Ланка", "Description": "Південноазійська країна"}, "ru": {"Name": "Шри-Ланка", "Description": "Южноазиатская страна"}}', false),
  ('Lesotho', 'LSO', 'LS', '{"en": {"Name": "Lesotho", "Description": "Southern African country"}, "cs": {"Name": "Lesotho", "Description": "Jihoafrická země"}, "sk": {"Name": "Lesotho", "Description": "Juhoafrická krajina"}, "uk": {"Name": "Лесото", "Description": "Південноафриканська країна"}, "ru": {"Name": "Лесото", "Description": "Южноафриканская страна"}}', false),
  ('Lithuania', 'LTU', 'LT', '{"en": {"Name": "Lithuania", "Description": "Northern European country"}, "cs": {"Name": "Litva", "Description": "Severní evropská země"}, "sk": {"Name": "Litva", "Description": "Severná európska krajina"}, "uk": {"Name": "Литва", "Description": "Північноєвропейська країна"}, "ru": {"Name": "Литва", "Description": "Северная европейская страна"}}', false),
  ('Luxembourg', 'LUX', 'LU', '{"en": {"Name": "Luxembourg", "Description": "Western European country"}, "cs": {"Name": "Lucembursko", "Description": "Západoevropská země"}, "sk": {"Name": "Luxembursko", "Description": "Západoeurópska krajina"}, "uk": {"Name": "Люксембург", "Description": "Західноєвропейська країна"}, "ru": {"Name": "Люксембург", "Description": "Западноевропейская страна"}}', false),
  ('Latvia', 'LVA', 'LV', '{"en": {"Name": "Latvia", "Description": "Northern European country"}, "cs": {"Name": "Lotyšsko", "Description": "Severní evropská země"}, "sk": {"Name": "Lotyšsko", "Description": "Severná európska krajina"}, "uk": {"Name": "Латвія", "Description": "Північноєвропейська країна"}, "ru": {"Name": "Латвия", "Description": "Северная европейская страна"}}', false),
  ('Macao SAR China', 'MAC', 'MO', '{"en": {"Name": "Macao SAR China", "Description": "East Asian country"}, "cs": {"Name": "Macao – ZAO Číny", "Description": "Východoasijská země"}, "sk": {"Name": "Macao – OAO Číny", "Description": "Východoázijská krajina"}, "uk": {"Name": "Макао, ОАР Китаю", "Description": "Східноазійська країна"}, "ru": {"Name": "Макао (САР)", "Description": "Восточноазиатская страна"}}', false),
  ('St. Martin', 'MAF', 'MF', '{"en": {"Name": "St. Martin", "Description": "Caribbean country"}, "cs": {"Name": "Svatý Martin (Francie)", "Description": "Karibská země"}, "sk": {"Name": "Svätý Martin (fr.)", "Description": "Karibská krajina"}, "uk": {"Name": "Сен-Мартен", "Description": "Карибська країна"}, "ru": {"Name": "Сен-Мартен", "Description": "Карибская страна"}}', false),
  ('Morocco', 'MAR', 'MA', '{"en": {"Name": "Morocco", "Description": "North African country"}, "cs": {"Name": "Maroko", "Description": "Severoafrická země"}, "sk": {"Name": "Maroko", "Description": "Severoafrická krajina"}, "uk": {"Name": "Марокко", "Description": "Північноафриканська країна"}, "ru": {"Name": "Марокко", "Description": "Североафриканская страна"}}', false),
  ('Monaco', 'MCO', 'MC', '{"en": {"Name": "Monaco", "Description": "Western European country"}, "cs": {"Name": "Monako", "Description": "Západoevropská země"}, "sk": {"Name": "Monako", "Description": "Západoeurópska krajina"}, "uk": {"Name": "Монако", "Description": "Західноєвропейська країна"}, "ru": {"Name": "Монако", "Description": "Западноевропейская страна"}}', false),
  ('Moldova', 'MDA', 'MD', '{"en": {"Name": "Moldova", "Description": "Eastern European country"}, "cs": {"Name": "Moldavsko", "Description": "Východoevropská země"}, "sk": {"Name": "Moldavsko", "Description": "Východoeurópska krajina"}, "uk": {"Name": "Молдова", "Description": "Східноєвропейська країна"}, "ru": {"Name": "Молдова", "Description": "Восточноевропейская страна"}}', false),
  ('Madagascar', 'MDG', 'MG', '{"en": {"Name": "Madagascar", "Description": "East African country"}, "cs": {"Name": "Madagaskar", "Description": "Východoafrická země"}, "sk": {"Name": "Madagaskar", "Description": "Východoafrická krajina"}, "uk": {"Name": "Мадагаскар", "Description": "Східноафриканська країна"}, "ru": {"Name": "Мадагаскар", "Description": "Восточноафриканская страна"}}', false),
  ('Maldives', 'MDV', 'MV', '{"en": {"Name": "Maldives", "Description": "South Asian country"}, "cs": {"Name": "Maledivy", "Description": "Jihoasijská země"}, "sk": {"Name": "Maldivy", "Description": "Juhoázijská krajina"}, "uk": {"Name": "Мальдіви", "Description": "Південноазійська країна"}, "ru": {"Name": "Мальдивы", "Description": "Южноазиатская страна"}}', false),
  ('Mexico', 'MEX', 'MX', '{"en": {"Name": "Mexico", "Description": "North American country"}, "cs": {"Name": "Mexiko", "Description": "Severoamerická země"}, "sk": {"Name": "Mexiko", "Description": "Severoamerická krajina"}, "uk": {"Name": "Мексика", "Description": "Північноамериканська країна"}, "ru": {"Name": "Мексика", "Description": "Североамериканская страна"}}', false),
  ('Marshall Islands', 'MHL', 'MH', '{"en": {"Name": "Marshall Islands", "Description": "Oceanic country"}, "cs": {"Name": "Marshallovy ostrovy", "Description": "Oceánská země"}, "sk": {"Name": "Marshallove ostrovy", "Description": "Oceánska krajina"}, "uk": {"Name": "Маршаллові Острови", "Description": "Океанійська країна"}, "ru": {"Name": "Маршалловы о-ва", "Description": "Океанская страна"}}', false),
  ('North Macedonia', 'MKD', 'MK', '{"en": {"Name": "North Macedonia", "Description": "Southeastern European country"}, "cs": {"Name": "Severní Makedonie", "Description": "Jihovýchodní evropská země"}, "sk": {"Name": "Severné Macedónsko", "Description": "Juhovýchodná európska krajina"}, "uk": {"Name": "Північна Македонія", "Description": "Південно-східна європейська країна"}, "ru": {"Name": "Северная Македония", "Description": "Юго-восточная европейская страна"}}', false),
  ('Mali', 'MLI', 'ML', '{"en": {"Name": "Mali", "Description": "West African country"}, "cs": {"Name": "Mali", "Description": "Západoafrická země"}, "sk": {"Name": "Mali", "Description": "Západoafrická krajina"}, "uk": {"Name": "Малі", "Description": "Західноафриканська країна"}, "ru": {"Name": "Мали", "Description": "Западноафриканская страна"}}', false),
  ('Malta', 'MLT', 'MT', '{"en": {"Name": "Malta", "Description": "Southern European country"}, "cs": {"Name": "Malta", "Description": "Jihoevropská země"}, "sk": {"Name": "Malta", "Description": "Juhoeurópska krajina"}, "uk": {"Name": "Мальта", "Description": "Південноєвропейська країна"}, "ru": {"Name": "Мальта", "Description": "Южноевропейская страна"}}', false),
  ('Myanmar (Burma)', 'MMR', 'MM', '{"en": {"Name": "Myanmar (Burma)", "Description": "Southeast Asian country"}, "cs": {"Name": "Myanmar (Barma)", "Description": "Jihovýchodní asijská země"}, "sk": {"Name": "Mjanmarsko", "Description": "Juhovýchodná ázijská krajina"}, "uk": {"Name": "Мʼянма (Бірма)", "Description": "Південно-східна азійська країна"}, "ru": {"Name": "Мьянма (Бирма)", "Description": "Юго-восточная азиатская страна"}}', false),
  ('Montenegro', 'MNE', 'ME', '{"en": {"Name": "Montenegro", "Description": "Southeastern European country"}, "cs": {"Name": "Černá Hora", "Description": "Jihovýchodní evropská země"}, "sk": {"Name": "Čierna Hora", "Description": "Juhovýchodná európska krajina"}, "uk": {"Name": "Чорногорія", "Description": "Південно-східна європейська країна"}, "ru": {"Name": "Черногория", "Description": "Юго-восточная европейская страна"}}', false),
  ('Mongolia', 'MNG', 'MN', '{"en": {"Name": "Mongolia", "Description": "East Asian country"}, "cs": {"Name": "Mongolsko", "Description": "Východoasijská země"}, "sk": {"Name": "Mongolsko", "Description": "Východoázijská krajina"}, "uk": {"Name": "Монголія", "Description": "Східноазійська країна"}, "ru": {"Name": "Монголия", "Description": "Восточноазиатская страна"}}', false),
  ('Northern Mariana Islands', 'MNP', 'MP', '{"en": {"Name": "Northern Mariana Islands", "Description": "Oceanic country"}, "cs": {"Name": "Severní Mariany", "Description": "Oceánská země"}, "sk": {"Name": "Severné Mariány", "Description": "Oceánska krajina"}, "uk": {"Name": "Північні Маріанські Острови", "Description": "Океанійська країна"}, "ru": {"Name": "Северные Марианские о-ва", "Description": "Океанская страна"}}', false),
  ('Mozambique', 'MOZ', 'MZ', '{"en": {"Name": "Mozambique", "Description": "East African country"}, "cs": {"Name": "Mosambik", "Description": "Východoafrická země"}, "sk": {"Name": "Mozambik", "Description": "Východoafrická krajina"}, "uk": {"Name": "Мозамбік", "Description": "Східноафриканська країна"}, "ru": {"Name": "Мозамбик", "Description": "Восточноафриканская страна"}}', false),
  ('Mauritania', 'MRT', 'MR', '{"en": {"Name": "Mauritania", "Description": "West African country"}, "cs": {"Name": "Mauritánie", "Description": "Západoafrická země"}, "sk": {"Name": "Mauritánia", "Description": "Západoafrická krajina"}, "uk": {"Name": "Мавританія", "Description": "Західноафриканська країна"}, "ru": {"Name": "Мавритания", "Description": "Западноафриканская страна"}}', false),
  ('Montserrat', 'MSR', 'MS', '{"en": {"Name": "Montserrat", "Description": "Caribbean country"}, "cs": {"Name": "Montserrat", "Description": "Karibská země"}, "sk": {"Name": "Montserrat", "Description": "Karibská krajina"}, "uk": {"Name": "Монтсеррат", "Description": "Карибська країна"}, "ru": {"Name": "Монтсеррат", "Description": "Карибская страна"}}', false),
  ('Martinique', 'MTQ', 'MQ', '{"en": {"Name": "Martinique", "Description": "Caribbean country"}, "cs": {"Name": "Martinik", "Description": "Karibská země"}, "sk": {"Name": "Martinik", "Description": "Karibská krajina"}, "uk": {"Name": "Мартиніка", "Description": "Карибська країна"}, "ru": {"Name": "Мартиника", "Description": "Карибская страна"}}', false),
  ('Mauritius', 'MUS', 'MU', '{"en": {"Name": "Mauritius", "Description": "East African country"}, "cs": {"Name": "Mauricius", "Description": "Východoafrická země"}, "sk": {"Name": "Maurícius", "Description": "Východoafrická krajina"}, "uk": {"Name": "Маврикій", "Description": "Східноафриканська країна"}, "ru": {"Name": "Маврикий", "Description": "Восточноафриканская страна"}}', false),
  ('Malawi', 'MWI', 'MW', '{"en": {"Name": "Malawi", "Description": "East African country"}, "cs": {"Name": "Malawi", "Description": "Východoafrická země"}, "sk": {"Name": "Malawi", "Description": "Východoafrická krajina"}, "uk": {"Name": "Малаві", "Description": "Східноафриканська країна"}, "ru": {"Name": "Малави", "Description": "Восточноафриканская страна"}}', false),
  ('Malaysia', 'MYS', 'MY', '{"en": {"Name": "Malaysia", "Description": "Southeast Asian country"}, "cs": {"Name": "Malajsie", "Description": "Jihovýchodní asijská země"}, "sk": {"Name": "Malajzia", "Description": "Juhovýchodná ázijská krajina"}, "uk": {"Name": "Малайзія", "Description": "Південно-східна азійська країна"}, "ru": {"Name": "Малайзия", "Description": "Юго-восточная азиатская страна"}}', false),
  ('Mayotte', 'MYT', 'YT', '{"en": {"Name": "Mayotte", "Description": "East African country"}, "cs": {"Name": "Mayotte", "Description": "Východoafrická země"}, "sk": {"Name": "Mayotte", "Description": "Východoafrická krajina"}, "uk": {"Name": "Майотта", "Description": "Східноафриканська країна"}, "ru": {"Name": "Майотта", "Description": "Восточноафриканская страна"}}', false),
  ('Namibia', 'NAM', 'NA', '{"en": {"Name": "Namibia", "Description": "Southern African country"}, "cs": {"Name": "Namibie", "Description": "Jihoafrická země"}, "sk": {"Name": "Namíbia", "Description": "Juhoafrická krajina"}, "uk": {"Name": "Намібія", "Description": "Південноафриканська країна"}, "ru": {"Name": "Намибия", "Description": "Южноафриканская страна"}}', false),
  ('New Caledonia', 'NCL', 'NC', '{"en": {"Name": "New Caledonia", "Description": "Oceanic country"}, "cs": {"Name": "Nová Kaledonie", "Description": "Oceánská země"}, "sk": {"Name": "Nová Kaledónia", "Description": "Oceánska krajina"}, "uk": {"Name": "Нова Каледонія", "Description": "Океанійська країна"}, "ru": {"Name": "Новая Каледония", "Description": "Океанская страна"}}', false),
  ('Niger', 'NER', 'NE', '{"en": {"Name": "Niger", "Description": "West African country"}, "cs": {"Name": "Niger", "Description": "Západoafrická země"}, "sk": {"Name": "Niger", "Description": "Západoafrická krajina"}, "uk": {"Name": "Нігер", "Description": "Західноафриканська країна"}, "ru": {"Name": "Нигер", "Description": "Западноафриканская страна"}}', false),
  ('Norfolk Island', 'NFK', 'NF', '{"en": {"Name": "Norfolk Island", "Description": "Oceanic country"}, "cs": {"Name": "Norfolk", "Description": "Oceánská země"}, "sk": {"Name": "Norfolk", "Description": "Oceánska krajina"}, "uk": {"Name": "Острів Норфолк", "Description": "Океанійська країна"}, "ru": {"Name": "о-в Норфолк", "Description": "Океанская страна"}}', false),
  ('Nigeria', 'NGA', 'NG', '{"en": {"Name": "Nigeria", "Description": "West African country"}, "cs": {"Name": "Nigérie", "Description": "Západoafrická země"}, "sk": {"Name": "Nigéria", "Description": "Západoafrická krajina"}, "uk": {"Name": "Нігерія", "Description": "Західноафриканська країна"}, "ru": {"Name": "Нигерия", "Description": "Западноафриканская страна"}}', false),
  ('Nicaragua', 'NIC', 'NI', '{"en": {"Name": "Nicaragua", "Description": "Central American country"}, "cs": {"Name": "Nikaragua", "Description": "Středoamerická země"}, "sk": {"Name": "Nikaragua", "Description": "Stredoamerická krajina"}, "uk": {"Name": "Нікарагуа", "Description": "Центральноамериканська країна"}, "ru": {"Name": "Никарагуа", "Description": "Центральноамериканская страна"}}', false),
  ('Niue', 'NIU', 'NU', '{"en": {"Name": "Niue", "Description": "Oceanic country"}, "cs": {"Name": "Niue", "Description": "Oceánská země"}, "sk": {"Name": "Niue", "Description": "Oceánska krajina"}, "uk": {"Name": "Ніуе", "Description": "Океанійська країна"}, "ru": {"Name": "Ниуэ", "Description": "Океанская страна"}}', false),
  ('Netherlands', 'NLD', 'NL', '{"en": {"Name": "Netherlands", "Description": "Northwestern European country"}, "cs": {"Name": "Nizozemsko", "Description": "Severozápadní evropská země"}, "sk": {"Name": "Holandsko", "Description": "Severozápadná európska krajina"}, "uk": {"Name": "Нідерланди", "Description": "Північно-західна європейська країна"}, "ru": {"Name": "Нидерланды", "Description": "Северо-западная европейская страна"}}', false),
  ('Norway', 'NOR', 'NO', '{"en": {"Name": "Norway", "Description": "Northern European country"}, "cs": {"Name": "Norsko", "Description": "Severní evropská země"}, "sk": {"Name": "Nórsko", "Description": "Severná európska krajina"}, "uk": {"Name": "Норвегія", "Description": "Північноєвропейська країна"}, "ru": {"Name": "Норвегия", "Description": "Северная европейская страна"}}', false),
  ('Nepal', 'NPL', 'NP', '{"en": {"Name": "Nepal", "Description": "South Asian country"}, "cs": {"Name": "Nepál", "Description": "Jihoasijská země"}, "sk": {"Name": "Nepál", "Description": "Juhoázijská krajina"}, "uk": {"Name": "Непал", "Description": "Південноазійська країна"}, "ru": {"Name": "Непал", "Description": "Южноазиатская страна"}}', false),
  ('Nauru', 'NRU', 'NR', '{"en": {"Name": "Nauru", "Description": "Oceanic country"}, "cs": {"Name": "Nauru", "Description": "Oceánská země"}, "sk": {"Name": "Nauru", "Description": "Oceánska krajina"}, "uk": {"Name": "Науру", "Description": "Океанійська країна"}, "ru": {"Name": "Науру", "Description": "Океанская страна"}}', false),
  ('New Zealand', 'NZL', 'NZ', '{"en": {"Name": "New Zealand", "Description": "Oceanic country"}, "cs": {"Name": "Nový Zéland", "Description": "Oceánská země"}, "sk": {"Name": "Nový Zéland", "Description": "Oceánska krajina"}, "uk": {"Name": "Нова Зеландія", "Description": "Океанійська країна"}, "ru": {"Name": "Новая Зеландия", "Description": "Океанская страна"}}', false),
  ('Oman', 'OMN', 'OM', '{"en": {"Name": "Oman", "Description": "Western Asian country"}, "cs": {"Name": "Omán", "Description": "Západoasijská země"}, "sk": {"Name": "Omán", "Description": "Západoázijská krajina"}, "uk": {"Name": "Оман", "Description": "Західноазійська країна"}, "ru": {"Name": "Оман", "Description": "Западноазиатская страна"}}', false),
  ('Pakistan', 'PAK', 'PK', '{"en": {"Name": "Pakistan", "Description": "South Asian country"}, "cs": {"Name": "Pákistán", "Description": "Jihoasijská země"}, "sk": {"Name": "Pakistan", "Description": "Juhoázijská krajina"}, "uk": {"Name": "Пакистан", "Description": "Південноазійська країна"}, "ru": {"Name": "Пакистан", "Description": "Южноазиатская страна"}}', false),
  ('Panama', 'PAN', 'PA', '{"en": {"Name": "Panama", "Description": "Central American country"}, "cs": {"Name": "Panama", "Description": "Středoamerická země"}, "sk": {"Name": "Panama", "Description": "Stredoamerická krajina"}, "uk": {"Name": "Панама", "Description": "Центральноамериканська країна"}, "ru": {"Name": "Панама", "Description": "Центральноамериканская страна"}}', false),
  ('Pitcairn Islands', 'PCN', 'PN', '{"en": {"Name": "Pitcairn Islands", "Description": "Oceanic country"}, "cs": {"Name": "Pitcairnovy ostrovy", "Description": "Oceánská země"}, "sk": {"Name": "Pitcairnove ostrovy", "Description": "Oceánska krajina"}, "uk": {"Name": "Острови Піткерн", "Description": "Океанійська країна"}, "ru": {"Name": "о-ва Питкэрн", "Description": "Океанская страна"}}', false),
  ('Peru', 'PER', 'PE', '{"en": {"Name": "Peru", "Description": "South American country"}, "cs": {"Name": "Peru", "Description": "Jihoamerická země"}, "sk": {"Name": "Peru", "Description": "Juhoamerická krajina"}, "uk": {"Name": "Перу", "Description": "Південноамериканська країна"}, "ru": {"Name": "Перу", "Description": "Южноамериканская страна"}}', false),
  ('Philippines', 'PHL', 'PH', '{"en": {"Name": "Philippines", "Description": "Southeast Asian country"}, "cs": {"Name": "Filipíny", "Description": "Jihovýchodní asijská země"}, "sk": {"Name": "Filipíny", "Description": "Juhovýchodná ázijská krajina"}, "uk": {"Name": "Філіппіни", "Description": "Південно-східна азійська країна"}, "ru": {"Name": "Филиппины", "Description": "Юго-восточная азиатская страна"}}', false),
  ('Palau', 'PLW', 'PW', '{"en": {"Name": "Palau", "Description": "Oceanic country"}, "cs": {"Name": "Palau", "Description": "Oceánská země"}, "sk": {"Name": "Palau", "Description": "Oceánska krajina"}, "uk": {"Name": "Палау", "Description": "Океанійська країна"}, "ru": {"Name": "Палау", "Description": "Океанская страна"}}', false),
  ('Papua New Guinea', 'PNG', 'PG', '{"en": {"Name": "Papua New Guinea", "Description": "Oceanic country"}, "cs": {"Name": "Papua-Nová Guinea", "Description": "Oceánská země"}, "sk": {"Name": "Papua-Nová Guinea", "Description": "Oceánska krajina"}, "uk": {"Name": "Папуа-Нова Гвінея", "Description": "Океанійська країна"}, "ru": {"Name": "Папуа — Новая Гвинея", "Description": "Океанская страна"}}', false),
  ('Poland', 'POL', 'PL', '{"en": {"Name": "Poland", "Description": "Central European country"}, "cs": {"Name": "Polsko", "Description": "Středoevropská země"}, "sk": {"Name": "Poľsko", "Description": "Stredoeurópska krajina"}, "uk": {"Name": "Польща", "Description": "Центральноєвропейська країна"}, "ru": {"Name": "Польша", "Description": "Центральноевропейская страна"}}', false),
  ('Puerto Rico', 'PRI', 'PR', '{"en": {"Name": "Puerto Rico", "Description": "Caribbean country"}, "cs": {"Name": "Portoriko", "Description": "Karibská země"}, "sk": {"Name": "Portoriko", "Description": "Karibská krajina"}, "uk": {"Name": "Пуерто-Рико", "Description": "Карибська країна"}, "ru": {"Name": "Пуэрто-Рико", "Description": "Карибская страна"}}', false),
  ('North Korea', 'PRK', 'KP', '{"en": {"Name": "North Korea", "Description": "East Asian country"}, "cs": {"Name": "Severní Korea", "Description": "Východoasijská země"}, "sk": {"Name": "Severná Kórea", "Description": "Východoázijská krajina"}, "uk": {"Name": "Північна Корея", "Description": "Східноазійська країна"}, "ru": {"Name": "КНДР", "Description": "Восточноазиатская страна"}}', false),
  ('Portugal', 'PRT', 'PT', '{"en": {"Name": "Portugal", "Description": "Southwestern European country"}, "cs": {"Name": "Portugalsko", "Description": "Jihozápadní evropská země"}, "sk": {"Name": "Portugalsko", "Description": "Juhozápadná európska krajina"}, "uk": {"Name": "Португалія", "Description": "Південно-західна європейська країна"}, "ru": {"Name": "Португалия", "Description": "Юго-западная европейская страна"}}', false),
  ('Paraguay', 'PRY', 'PY', '{"en": {"Name": "Paraguay", "Description": "South American country"}, "cs": {"Name": "Paraguay", "Description": "Jihoamerická země"}, "sk": {"Name": "Paraguaj", "Description": "Juhoamerická krajina"}, "uk": {"Name": "Парагвай", "Description": "Південноамериканська країна"}, "ru": {"Name": "Парагвай", "Description": "Южноамериканская страна"}}', false),
  ('Palestinian Territories', 'PSE', 'PS', '{"en": {"Name": "Palestinian Territories", "Description": "Western Asian country"}, "cs": {"Name": "Palestinská území", "Description": "Západoasijská země"}, "sk": {"Name": "Palestínske územia", "Description": "Západoázijská krajina"}, "uk": {"Name": "Палестинські території", "Description": "Західноазійська країна"}, "ru": {"Name": "Палестинские территории", "Description": "Западноазиатская страна"}}', false),
  ('French Polynesia', 'PYF', 'PF', '{"en": {"Name": "French Polynesia", "Description": "Oceanic country"}, "cs": {"Name": "Francouzská Polynésie", "Description": "Oceánská země"}, "sk": {"Name": "Francúzska Polynézia", "Description": "Oceánska krajina"}, "uk": {"Name": "Французька Полінезія", "Description": "Океанійська країна"}, "ru": {"Name": "Французская Полинезия", "Description": "Океанская страна"}}', false),
  ('Qatar', 'QAT', 'QA', '{"en": {"Name": "Qatar", "Description": "Western Asian country"}, "cs": {"Name": "Katar", "Description": "Západoasijská země"}, "sk": {"Name": "Katar", "Description": "Západoázijská krajina"}, "uk": {"Name": "Катар", "Description": "Західноазійська країна"}, "ru": {"Name": "Катар", "Description": "Западноазиатская страна"}}', false),
  ('Réunion', 'REU', 'RE', '{"en": {"Name": "Réunion", "Description": "East African country"}, "cs": {"Name": "Réunion", "Description": "Východoafrická země"}, "sk": {"Name": "Réunion", "Description": "Východoafrická krajina"}, "uk": {"Name": "Реюньйон", "Description": "Східноафриканська країна"}, "ru": {"Name": "Реюньон", "Description": "Восточноафриканская страна"}}', false),
  ('Romania', 'ROU', 'RO', '{"en": {"Name": "Romania", "Description": "Southeastern European country"}, "cs": {"Name": "Rumunsko", "Description": "Jihovýchodní evropská země"}, "sk": {"Name": "Rumunsko", "Description": "Juhovýchodná európska krajina"}, "uk": {"Name": "Румунія", "Description": "Південно-східна європейська країна"}, "ru": {"Name": "Румыния", "Description": "Юго-восточная европейская страна"}}', false),
  ('Russia', 'RUS', 'RU', '{"en": {"Name": "Russia", "Description": "Eurasian country"}, "cs": {"Name": "Rusko", "Description": "Euroasijská země"}, "sk": {"Name": "Rusko", "Description": "Euroázijská krajina"}, "uk": {"Name": "Росія", "Description": "Євразійська країна"}, "ru": {"Name": "Россия", "Description": "Евразийская страна"}}', false),
  ('Rwanda', 'RWA', 'RW', '{"en": {"Name": "Rwanda", "Description": "East African country"}, "cs": {"Name": "Rwanda", "Description": "Východoafrická země"}, "sk": {"Name": "Rwanda", "Description": "Východoafrická krajina"}, "uk": {"Name": "Руанда", "Description": "Східноафриканська країна"}, "ru": {"Name": "Руанда", "Description": "Восточноафриканская страна"}}', false),
  ('Saudi Arabia', 'SAU', 'SA', '{"en": {"Name": "Saudi Arabia", "Description": "Western Asian country"}, "cs": {"Name": "Saúdská Arábie", "Description": "Západoasijská země"}, "sk": {"Name": "Saudská Arábia", "Description": "Západoázijská krajina"}, "uk": {"Name": "Саудівська Аравія", "Description": "Західноазійська країна"}, "ru": {"Name": "Саудовская Аравия", "Description": "Западноазиатская страна"}}', false),
  ('Sudan', 'SDN', 'SD', '{"en": {"Name": "Sudan", "Description": "North African country"}, "cs": {"Name": "Súdán", "Description": "Severoafrická země"}, "sk": {"Name": "Sudán", "Description": "Severoafrická krajina"}, "uk": {"Name": "Судан", "Description": "Північноафриканська країна"}, "ru": {"Name": "Судан", "Description": "Североафриканская страна"}}', false),
  ('Senegal', 'SEN', 'SN', '{"en": {"Name": "Senegal", "Description": "West African country"}, "cs": {"Name": "Senegal", "Description": "Západoafrická země"}, "sk": {"Name": "Senegal", "Description": "Západoafrická krajina"}, "uk": {"Name": "Сенегал", "Description": "Західноафриканська країна"}, "ru": {"Name": "Сенегал", "Description": "Западноафриканская страна"}}', false),
  ('Singapore', 'SGP', 'SG', '{"en": {"Name": "Singapore", "Description": "Southeast Asian country"}, "cs": {"Name": "Singapur", "Description": "Jihovýchodní asijská země"}, "sk": {"Name": "Singapur", "Description": "Juhovýchodná ázijská krajina"}, "uk": {"Name": "Сінгапур", "Description": "Південно-східна азійська країна"}, "ru": {"Name": "Сингапур", "Description": "Юго-восточная азиатская страна"}}', false),
  ('South Georgia & South Sandwich Islands', 'SGS', 'GS', '{"en": {"Name": "South Georgia & South Sandwich Islands", "Description": "Antarctic territory"}, "cs": {"Name": "Jižní Georgie a Jižní Sandwichovy ostrovy", "Description": "Antarktické území"}, "sk": {"Name": "Južná Georgia a Južné Sandwichove ostrovy", "Description": "Antarktické územie"}, "uk": {"Name": "Південна Джорджія та Південні Сандвічеві Острови", "Description": "Антарктична територія"}, "ru": {"Name": "Южная Георгия и Южные Сандвичевы о-ва", "Description": "Антарктическая территория"}}', false),
  ('St. Helena', 'SHN', 'SH', '{"en": {"Name": "St. Helena", "Description": "West African country"}, "cs": {"Name": "Svatá Helena", "Description": "Západoafrická země"}, "sk": {"Name": "Svätá Helena", "Description": "Západoafrická krajina"}, "uk": {"Name": "Острів Святої Єлени", "Description": "Західноафриканська країна"}, "ru": {"Name": "о-в Св. Елены", "Description": "Западноафриканская страна"}}', false),
  ('Svalbard & Jan Mayen', 'SJM', 'SJ', '{"en": {"Name": "Svalbard & Jan Mayen", "Description": "Northern European country"}, "cs": {"Name": "Špicberky a Jan Mayen", "Description": "Severní evropská země"}, "sk": {"Name": "Svalbard a Jan Mayen", "Description": "Severná európska krajina"}, "uk": {"Name": "Шпіцберген та Ян-Маєн", "Description": "Північноєвропейська країна"}, "ru": {"Name": "Шпицберген и Ян-Майен", "Description": "Северная европейская страна"}}', false),
  ('Solomon Islands', 'SLB', 'SB', '{"en": {"Name": "Solomon Islands", "Description": "Oceanic country"}, "cs": {"Name": "Šalamounovy ostrovy", "Description": "Oceánská země"}, "sk": {"Name": "Šalamúnove ostrovy", "Description": "Oceánska krajina"}, "uk": {"Name": "Соломонові Острови", "Description": "Океанійська країна"}, "ru": {"Name": "Соломоновы о-ва", "Description": "Океанская страна"}}', false),
  ('Sierra Leone', 'SLE', 'SL', '{"en": {"Name": "Sierra Leone", "Description": "West African country"}, "cs": {"Name": "Sierra Leone", "Description": "Západoafrická země"}, "sk": {"Name": "Sierra Leone", "Description": "Západoafrická krajina"}, "uk": {"Name": "Сьєрра-Леоне", "Description": "Західноафриканська країна"}, "ru": {"Name": "Сьерра-Леоне", "Description": "Западноафриканская страна"}}', false),
  ('El Salvador', 'SLV', 'SV', '{"en": {"Name": "El Salvador", "Description": "Central American country"}, "cs": {"Name": "Salvador", "Description": "Středoamerická země"}, "sk": {"Name": "Salvádor", "Description": "Stredoamerická krajina"}, "uk": {"Name": "Сальвадор", "Description": "Центральноамериканська країна"}, "ru": {"Name": "Сальвадор", "Description": "Центральноамериканская страна"}}', false),
  ('San Marino', 'SMR', 'SM', '{"en": {"Name": "San Marino", "Description": "Southern European country"}, "cs": {"Name": "San Marino", "Description": "Jihoevropská země"}, "sk": {"Name": "San Maríno", "Description": "Juhoeurópska krajina"}, "uk": {"Name": "Сан-Марино", "Description": "Південноєвропейська країна"}, "ru": {"Name": "Сан-Марино", "Description": "Южноевропейская страна"}}', false),
  ('Somalia', 'SOM', 'SO', '{"en": {"Name": "Somalia", "Description": "East African country"}, "cs": {"Name": "Somálsko", "Description": "Východoafrická země"}, "sk": {"Name": "Somálsko", "Description": "Východoafrická krajina"}, "uk": {"Name": "Сомалі", "Description": "Східноафриканська країна"}, "ru": {"Name": "Сомали", "Description": "Восточноафриканская страна"}}', false),
  ('St. Pierre & Miquelon', 'SPM', 'PM', '{"en": {"Name": "St. Pierre & Miquelon", "Description": "North American country"}, "cs": {"Name": "Saint-Pierre a Miquelon", "Description": "Severoamerická země"}, "sk": {"Name": "Saint Pierre a Miquelon", "Description": "Severoamerická krajina"}, "uk": {"Name": "Сен-Пʼєр і Мікелон", "Description": "Північноамериканська країна"}, "ru": {"Name": "Сен-Пьер и Микелон", "Description": "Североамериканская страна"}}', false),
  ('Serbia', 'SRB', 'RS', '{"en": {"Name": "Serbia", "Description": "Southeastern European country"}, "cs": {"Name": "Srbsko", "Description": "Jihovýchodní evropská země"}, "sk": {"Name": "Srbsko", "Description": "Juhovýchodná európska krajina"}, "uk": {"Name": "Сербія", "Description": "Південно-східна європейська країна"}, "ru": {"Name": "Сербия", "Description": "Юго-восточная европейская страна"}}', false),
  ('South Sudan', 'SSD', 'SS', '{"en": {"Name": "South Sudan", "Description": "East African country"}, "cs": {"Name": "Jižní Súdán", "Description": "Východoafrická země"}, "sk": {"Name": "Južný Sudán", "Description": "Východoafrická krajina"}, "uk": {"Name": "Південний Судан", "Description": "Східноафриканська країна"}, "ru": {"Name": "Южный Судан", "Description": "Восточноафриканская страна"}}', false),
  ('São Tomé & Príncipe', 'STP', 'ST', '{"en": {"Name": "São Tomé & Príncipe", "Description": "Central African country"}, "cs": {"Name": "Svatý Tomáš a Princův ostrov", "Description": "Středoafrická země"}, "sk": {"Name": "Svätý Tomáš a Princov ostrov", "Description": "Stredoafrická krajina"}, "uk": {"Name": "Сан-Томе і Принсіпі", "Description": "Центральноафриканська країна"}, "ru": {"Name": "Сан-Томе и Принсипи", "Description": "Центральноафриканская страна"}}', false),
  ('Suriname', 'SUR', 'SR', '{"en": {"Name": "Suriname", "Description": "South American country"}, "cs": {"Name": "Surinam", "Description": "Jihoamerická země"}, "sk": {"Name": "Surinam", "Description": "Juhoamerická krajina"}, "uk": {"Name": "Суринам", "Description": "Південноамериканська країна"}, "ru": {"Name": "Суринам", "Description": "Южноамериканская страна"}}', false),
  ('Slovakia', 'SVK', 'SK', '{"en": {"Name": "Slovakia", "Description": "Central European country"}, "cs": {"Name": "Slovensko", "Description": "Středoevropská země"}, "sk": {"Name": "Slovensko", "Description": "Stredoeurópska krajina"}, "uk": {"Name": "Словаччина", "Description": "Центральноєвропейська країна"}, "ru": {"Name": "Словакия", "Description": "Центральноевропейская страна"}}', false),
  ('Slovenia', 'SVN', 'SI', '{"en": {"Name": "Slovenia", "Description": "Central European country"}, "cs": {"Name": "Slovinsko", "Description": "Středoevropská země"}, "sk": {"Name": "Slovinsko", "Description": "Stredoeurópska krajina"}, "uk": {"Name": "Словенія", "Description": "Центральноєвропейська країна"}, "ru": {"Name": "Словения", "Description": "Центральноевропейская страна"}}', false),
  ('Sweden', 'SWE', 'SE', '{"en": {"Name": "Sweden", "Description": "Northern European country"}, "cs": {"Name": "Švédsko", "Description": "Severní evropská země"}, "sk": {"Name": "Švédsko", "Description": "Severná európska krajina"}, "uk": {"Name": "Швеція", "Description": "Північноєвропейська країна"}, "ru": {"Name": "Швеция", "Description": "Северная европейская страна"}}', false),
  ('Eswatini', 'SWZ', 'SZ', '{"en": {"Name": "Eswatini", "Description": "Southern African country"}, "cs": {"Name": "Eswatini", "Description": "Jihoafrická země"}, "sk": {"Name": "Eswatini", "Description": "Juhoafrická krajina"}, "uk": {"Name": "Есватіні", "Description": "Південноафриканська країна"}, "ru": {"Name": "Эсватини", "Description": "Южноафриканская страна"}}', false),
  ('Sint Maarten', 'SXM', 'SX', '{"en": {"Name": "Sint Maarten", "Description": "Caribbean country"}, "cs": {"Name": "Svatý Martin (Nizozemsko)", "Description": "Karibská země"}, "sk": {"Name": "Svätý Martin (hol.)", "Description": "Karibská krajina"}, "uk": {"Name": "Сінт-Мартен", "Description": "Карибська країна"}, "ru": {"Name": "Синт-Мартен", "Description": "Карибская страна"}}', false),
  ('Seychelles', 'SYC', 'SC', '{"en": {"Name": "Seychelles", "Description": "East African country"}, "cs": {"Name": "Seychely", "Description": "Východoafrická země"}, "sk": {"Name": "Seychely", "Description": "Východoafrická krajina"}, "uk": {"Name": "Сейшельські Острови", "Description": "Східноафриканська країна"}, "ru": {"Name": "Сейшельские о-ва", "Description": "Восточноафриканская страна"}}', false),
  ('Syria', 'SYR', 'SY', '{"en": {"Name": "Syria", "Description": "Western Asian country"}, "cs": {"Name": "Sýrie", "Description": "Západoasijská země"}, "sk": {"Name": "Sýria", "Description": "Západoázijská krajina"}, "uk": {"Name": "Сирія", "Description": "Західноазійська країна"}, "ru": {"Name": "Сирия", "Description": "Западноазиатская страна"}}', false),
  ('Turks & Caicos Islands', 'TCA', 'TC', '{"en": {"Name": "Turks & Caicos Islands", "Description": "Caribbean country"}, "cs": {"Name": "Turks a Caicos", "Description": "Karibská země"}, "sk": {"Name": "Turks a Caicos", "Description": "Karibská krajina"}, "uk": {"Name": "Острови Теркс і Кайкос", "Description": "Карибська країна"}, "ru": {"Name": "Тёркс и Кайкос", "Description": "Карибская страна"}}', false),
  ('Chad', 'TCD', 'TD', '{"en": {"Name": "Chad", "Description": "Central African country"}, "cs": {"Name": "Čad", "Description": "Středoafrická země"}, "sk": {"Name": "Čad", "Description": "Stredoafrická krajina"}, "uk": {"Name": "Чад", "Description": "Центральноафриканська країна"}, "ru": {"Name": "Чад", "Description": "Центральноафриканская страна"}}', false),
  ('Togo', 'TGO', 'TG', '{"en": {"Name": "Togo", "Description": "West African country"}, "cs": {"Name": "Togo", "Description": "Západoafrická země"}, "sk": {"Name": "Togo", "Description": "Západoafrická krajina"}, "uk": {"Name": "Того", "Description": "Західноафриканська країна"}, "ru": {"Name": "Того", "Description": "Западноафриканская страна"}}', false),
  ('Thailand', 'THA', 'TH', '{"en": {"Name": "Thailand", "Description": "Southeast Asian country"}, "cs": {"Name": "Thajsko", "Description": "Jihovýchodní asijská země"}, "sk": {"Name": "Thajsko", "Description": "Juhovýchodná ázijská krajina"}, "uk": {"Name": "Таїланд", "Description": "Південно-східна азійська країна"}, "ru": {"Name": "Таиланд", "Description": "Юго-восточная азиатская страна"}}', false),
  ('Tajikistan', 'TJK', 'TJ', '{"en": {"Name": "Tajikistan", "Description": "Central Asian country"}, "cs": {"Name": "Tádžikistán", "Description": "Středoasijská země"}, "sk": {"Name": "Tadžikistan", "Description": "Stredoázijská krajina"}, "uk": {"Name": "Таджикистан", "Description": "Центральноазійська країна"}, "ru": {"Name": "Таджикистан", "Description": "Центральноазиатская страна"}}', false),
  ('Tokelau', 'TKL', 'TK', '{"en": {"Name": "Tokelau", "Description": "Oceanic country"}, "cs": {"Name": "Tokelau", "Description": "Oceánská země"}, "sk": {"Name": "Tokelau", "Description": "Oceánska krajina"}, "uk": {"Name": "Токелау", "Description": "Океанійська країна"}, "ru": {"Name": "Токелау", "Description": "Океанская страна"}}', false),
  ('Turkmenistan', 'TKM', 'TM', '{"en": {"Name": "Turkmenistan", "Description": "Central Asian country"}, "cs": {"Name": "Turkmenistán", "Description": "Středoasijská země"}, "sk": {"Name": "Turkménsko", "Description": "Stredoázijská krajina"}, "uk": {"Name": "Туркменістан", "Description": "Центральноазійська країна"}, "ru": {"Name": "Туркменистан", "Description": "Центральноазиатская страна"}}', false),
  ('Timor-Leste', 'TLS', 'TL', '{"en": {"Name": "Timor-Leste", "Description": "Southeast Asian country"}, "cs": {"Name": "Východní Timor", "Description": "Jihovýchodní asijská země"}, "sk": {"Name": "Východný Timor", "Description": "Juhovýchodná ázijská krajina"}, "uk": {"Name": "Тимор-Лешті", "Description": "Південно-східна азійська країна"}, "ru": {"Name": "Восточный Тимор", "Description": "Юго-восточная азиатская страна"}}', false),
  ('Tonga', 'TON', 'TO', '{"en": {"Name": "Tonga", "Description": "Oceanic country"}, "cs": {"Name": "Tonga", "Description": "Oceánská země"}, "sk": {"Name": "Tonga", "Description": "Oceánska krajina"}, "uk": {"Name": "Тонга", "Description": "Океанійська країна"}, "ru": {"Name": "Тонга", "Description": "Океанская страна"}}', false),
  ('Trinidad & Tobago', 'TTO', 'TT', '{"en": {"Name": "Trinidad & Tobago", "Description": "Caribbean country"}, "cs": {"Name": "Trinidad a Tobago", "Description": "Karibská země"}, "sk": {"Name": "Trinidad a Tobago", "Description": "Karibská krajina"}, "uk": {"Name": "Тринідад і Тобаго", "Description": "Карибська країна"}, "ru": {"Name": "Тринидад и Тобаго", "Description": "Карибская страна"}}', false),
  ('Tunisia', 'TUN', 'TN', '{"en": {"Name": "Tunisia", "Description": "North African country"}, "cs": {"Name": "Tunisko", "Description": "Severoafrická země"}, "sk": {"Name": "Tunisko", "Description": "Severoafrická krajina"}, "uk": {"Name": "Туніс", "Description": "Північноафриканська країна"}, "ru": {"Name": "Тунис", "Description": "Североафриканская страна"}}', false),
  ('Türkiye', 'TUR', 'TR', '{"en": {"Name": "Türkiye", "Description": "Eurasian country"}, "cs": {"Name": "Turecko", "Description": "Euroasijská země"}, "sk": {"Name": "Turecko", "Description": "Euroázijská krajina"}, "uk": {"Name": "Туреччина", "Description": "Євразійська країна"}, "ru": {"Name": "Турция", "Description": "Евразийская страна"}}', false),
  ('Tuvalu', 'TUV', 'TV', '{"en": {"Name": "Tuvalu", "Description": "Oceanic country"}, "cs": {"Name": "Tuvalu", "Description": "Oceánská země"}, "sk": {"Name": "Tuvalu", "Description": "Oceánska krajina"}, "uk": {"Name": "Тувалу", "Description": "Океанійська країна"}, "ru": {"Name": "Тувалу", "Description": "Океанская страна"}}', false),
  ('Taiwan', 'TWN', 'TW', '{"en": {"Name": "Taiwan", "Description": "East Asian country"}, "cs": {"Name": "Tchaj-wan", "Description": "Východoasijská země"}, "sk": {"Name": "Taiwan", "Description": "Východoázijská krajina"}, "uk": {"Name": "Тайвань", "Description": "Східноазійська країна"}, "ru": {"Name": "Тайвань", "Description": "Восточноазиатская страна"}}', false),
  ('Tanzania', 'TZA', 'TZ', '{"en": {"Name": "Tanzania", "Description": "East African country"}, "cs": {"Name": "Tanzanie", "Description": "Východoafrická země"}, "sk": {"Name": "Tanzánia", "Description": "Východoafrická krajina"}, "uk": {"Name": "Танзанія", "Description": "Східноафриканська країна"}, "ru": {"Name": "Танзания", "Description": "Восточноафриканская страна"}}', false),
  ('Uganda', 'UGA', 'UG', '{"en": {"Name": "Uganda", "Description": "East African country"}, "cs": {"Name": "Uganda", "Description": "Východoafrická země"}, "sk": {"Name": "Uganda", "Description": "Východoafrická krajina"}, "uk": {"Name": "Уганда", "Description": "Східноафриканська країна"}, "ru": {"Name": "Уганда", "Description": "Восточноафриканская страна"}}', false),
  ('Ukraine', 'UKR', 'UA', '{"en": {"Name": "Ukraine", "Description": "Eastern European country"}, "cs": {"Name": "Ukrajina", "Description": "Východoevropská země"}, "sk": {"Name": "Ukrajina", "Description": "Východoeurópska krajina"}, "uk": {"Name": "Україна", "Description": "Східноєвропейська країна"}, "ru": {"Name": "Украина", "Description": "Восточноевропейская страна"}}', false),
  ('U.S. Outlying Islands', 'UMI', 'UM', '{"en": {"Name": "U.S. Outlying Islands", "Description": "Oceanic country"}, "cs": {"Name": "Menší odlehlé ostrovy USA", "Description": "Oceánská země"}, "sk": {"Name": "Menšie odľahlé ostrovy USA", "Description": "Oceánska krajina"}, "uk": {"Name": "Віддалені острови США", "Description": "Океанійська країна"}, "ru": {"Name": "Внешние малые о-ва (США)", "Description": "Океанская страна"}}', false),
  ('Uruguay', 'URY', 'UY', '{"en": {"Name": "Uruguay", "Description": "South American country"}, "cs": {"Name": "Uruguay", "Description": "Jihoamerická země"}, "sk": {"Name": "Uruguaj", "Description": "Juhoamerická krajina"}, "uk": {"Name": "Уругвай", "Description": "Південноамериканська країна"}, "ru": {"Name": "Уругвай", "Description": "Южноамериканская страна"}}', false),
  ('United States', 'USA', 'US', '{"en": {"Name": "United States", "Description": "North American country"}, "cs": {"Name": "Spojené státy", "Description": "Severoamerická země"}, "sk": {"Name": "Spojené štáty", "Description": "Severoamerická krajina"}, "uk": {"Name": "Сполучені Штати", "Description": "Північноамериканська країна"}, "ru": {"Name": "Соединенные Штаты", "Description": "Североамериканская страна"}}', false),
  ('Uzbekistan', 'UZB', 'UZ', '{"en": {"Name": "Uzbekistan", "Description": "Central Asian country"}, "cs": {"Name": "Uzbekistán", "Description": "Středoasijská země"}, "sk": {"Name": "Uzbekistan", "Description": "Stredoázijská krajina"}, "uk": {"Name": "Узбекистан", "Description": "Центральноазійська країна"}, "ru": {"Name": "Узбекистан", "Description": "Центральноазиатская страна"}}', false),
  ('Vatican City', 'VAT', 'VA', '{"en": {"Name": "Vatican City", "Description": "Southern European country"}, "cs": {"Name": "Vatikán", "Description": "Jihoevropská země"}, "sk": {"Name": "Vatikán", "Description": "Juhoeurópska krajina"}, "uk": {"Name": "Ватикан", "Description": "Південноєвропейська країна"}, "ru": {"Name": "Ватикан", "Description": "Южноевропейская страна"}}', false),
  ('St. Vincent & Grenadines', 'VCT', 'VC', '{"en": {"Name": "St. Vincent & Grenadines", "Description": "Caribbean country"}, "cs": {"Name": "Svatý Vincenc a Grenadiny", "Description": "Karibská země"}, "sk": {"Name": "Svätý Vincent a Grenadíny", "Description": "Karibská krajina"}, "uk": {"Name": "Сент-Вінсент і Гренадіни", "Description": "Карибська країна"}, "ru": {"Name": "Сент-Винсент и Гренадины", "Description": "Карибская страна"}}', false),
  ('Venezuela', 'VEN', 'VE', '{"en": {"Name": "Venezuela", "Description": "South American country"}, "cs": {"Name": "Venezuela", "Description": "Jihoamerická země"}, "sk": {"Name": "Venezuela", "Description": "Juhoamerická krajina"}, "uk": {"Name": "Венесуела", "Description": "Південноамериканська країна"}, "ru": {"Name": "Венесуэла", "Description": "Южноамериканская страна"}}', false),
  ('British Virgin Islands', 'VGB', 'VG', '{"en": {"Name": "British Virgin Islands", "Description": "Caribbean country"}, "cs": {"Name": "Britské Panenské ostrovy", "Description": "Karibská země"}, "sk": {"Name": "Britské Panenské ostrovy", "Description": "Karibská krajina"}, "uk": {"Name": "Британські Віргінські острови", "Description": "Карибська країна"}, "ru": {"Name": "Виргинские о-ва (Великобритания)", "Description": "Карибская страна"}}', false),
  ('U.S. Virgin Islands', 'VIR', 'VI', '{"en": {"Name": "U.S. Virgin Islands", "Description": "Caribbean country"}, "cs": {"Name": "Americké Panenské ostrovy", "Description": "Karibská země"}, "sk": {"Name": "Americké Panenské ostrovy", "Description": "Karibská krajina"}, "uk": {"Name": "Віргінські Острови (США)", "Description": "Карибська країна"}, "ru": {"Name": "Виргинские о-ва (США)", "Description": "Карибская страна"}}', false),
  ('Vietnam', 'VNM', 'VN', '{"en": {"Name": "Vietnam", "Description": "Southeast Asian country"}, "cs": {"Name": "Vietnam", "Description": "Jihovýchodní asijská země"}, "sk": {"Name": "Vietnam", "Description": "Juhovýchodná ázijská krajina"}, "uk": {"Name": "Вʼєтнам", "Description": "Південно-східна азійська країна"}, "ru": {"Name": "Вьетнам", "Description": "Юго-восточная азиатская страна"}}', false),
  ('Vanuatu', 'VUT', 'VU', '{"en": {"Name": "Vanuatu", "Description": "Oceanic country"}, "cs": {"Name": "Vanuatu", "Description": "Oceánská země"}, "sk": {"Name": "Vanuatu", "Description": "Oceánska krajina"}, "uk": {"Name": "Вануату", "Description": "Океанійська країна"}, "ru": {"Name": "Вануату", "Description": "Океанская страна"}}', false),
  ('Wallis & Futuna', 'WLF', 'WF', '{"en": {"Name": "Wallis & Futuna", "Description": "Oceanic country"}, "cs": {"Name": "Wallis a Futuna", "Description": "Oceánská země"}, "sk": {"Name": "Wallis a Futuna", "Description": "Oceánska krajina"}, "uk": {"Name": "Уолліс і Футуна", "Description": "Океанійська країна"}, "ru": {"Name": "Уоллис и Футуна", "Description": "Океанская страна"}}', false),
  ('Samoa', 'WSM', 'WS', '{"en": {"Name": "Samoa", "Description": "Oceanic country"}, "cs": {"Name": "Samoa", "Description": "Oceánská země"}, "sk": {"Name": "Samoa", "Description": "Oceánska krajina"}, "uk": {"Name": "Самоа", "Description": "Океанійська країна"}, "ru": {"Name": "Самоа", "Description": "Океанская страна"}}', false),
  ('Yemen', 'YEM', 'YE', '{"en": {"Name": "Yemen", "Description": "Western Asian country"}, "cs": {"Name": "Jemen", "Description": "Západoasijská země"}, "sk": {"Name": "Jemen", "Description": "Západoázijská krajina"}, "uk": {"Name": "Ємен", "Description": "Західноазійська країна"}, "ru": {"Name": "Йемен", "Description": "Западноазиатская страна"}}', false),
  ('South Africa', 'ZAF', 'ZA', '{"en": {"Name": "South Africa", "Description": "Southern African country"}, "cs": {"Name": "Jižní Afrika", "Description": "Jihoafrická země"}, "sk": {"Name": "Južná Afrika", "Description": "Juhoafrická krajina"}, "uk": {"Name": "Південно-Африканська Республіка", "Description": "Південноафриканська країна"}, "ru": {"Name": "Южная Африка", "Description": "Южноафриканская страна"}}', false),
  ('Zambia', 'ZMB', 'ZM', '{"en": {"Name": "Zambia", "Description": "East African country"}, "cs": {"Name": "Zambie", "Description": "Východoafrická země"}, "sk": {"Name": "Zambia", "Description": "Východoafrická krajina"}, "uk": {"Name": "Замбія", "Description": "Східноафриканська країна"}, "ru": {"Name": "Замбия", "Description": "Восточноафриканская страна"}}', false),
  ('Zimbabwe', 'ZWE', 'ZW', '{"en": {"Name": "Zimbabwe", "Description": "East African country"}, "cs": {"Name": "Zimbabwe", "Description": "Východoafrická země"}, "sk": {"Name": "Zimbabwe", "Description": "Východoafrická krajina"}, "uk": {"Name": "Зімбабве", "Description": "Східноафриканська країна"}, "ru": {"Name": "Зимбабве", "Description": "Восточноафриканская страна"}}', false)
) AS v(name, iso_code, iso_alpha2, translations, is_serviced)
WHERE NOT EXISTS (SELECT 1 FROM public."Countries" c WHERE c."IsoCode" = v.iso_code);

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
  "CountryId", "Name", "ZipPrefix"
)
SELECT generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
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
) AS city(name, zip_prefix)
WHERE NOT EXISTS (
  SELECT 1
  FROM public."ServiceCities" sc
  WHERE sc."CountryId" = (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1)
    AND LOWER(sc."Name") = LOWER(city.name)
);

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
  "CountryId", "Name", "ZipPrefix"
)
SELECT generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
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
);

-- 5. CURRENCIES
-- CZK only (decision 76): the one currency the platform operates in, and the default.
--
-- LoyaltyPointsDivisor: the amount that earns one point — the historical "1 point per 10 CZK".
-- ActivateCurrency refuses a currency without one, so an active currency always carries it.
--
-- NoShowCredit: the apology credit CancelUnfilledOrders pays on an order in this currency when its slot
-- arrives with no cleaner (owner ruling 2026-09-05: 250 CZK). NULL would mean no credit in that
-- currency -- the sweep refunds in full and sends the plain cancellation.
INSERT INTO public."Currencies" (
  "Id", "IsActive", "IsDefault", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Code", "Symbol", "Name", "LoyaltyPointsDivisor", "NoShowCredit"
)
VALUES
  (generate_ulid()::TEXT, true, true,  'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL, 'CZK', 'Kč', 'Czech Koruna', 10.00, 250.00)
ON CONFLICT ("Code") DO NOTHING;

-- ============================================================
-- COUNTRY INVOICE CONFIGS
-- ============================================================
-- "LegalDisclaimerTemplate" is deliberately absent from this INSERT: every row starts with NO legal
-- notice and LegalDisclaimerReviewStatus = 0 (NotReviewed), so the invoice prints the platform's
-- generic English fallback. Only the UPDATE at the bottom of this section fills one in.
-- The review status is written EXPLICITLY: the column is NOT NULL and carries no database default, so
-- omitting it aborts the whole seed transaction with 23502 and a fresh database comes up empty.
INSERT INTO public."CountryInvoiceConfigs" (
  "Id", "IsActive", "CountryId", "VatRequired", "VatRate",
  "DigitalSignatureRequired", "EInvoiceFormat",
  "AdditionalFieldsJson", "LegalDisclaimerReviewStatus"
)
VALUES
  -- Czech Republic - VAT 21%
  (generate_ulid()::TEXT, true,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1),
   true, 0.21, false, 'PDF', NULL, 0)
ON CONFLICT ("CountryId") DO NOTHING;

-- Konstantní symbol — the PAYER's payment-type code on a Czech bank transfer (0308 = non-cash payment
-- for goods and services), which is why it is configured here and not on a cleaner's bank record.
-- Countries whose payment system has no such code stay NULL and the field is omitted from the invoice.
UPDATE public."CountryInvoiceConfigs" SET "ConstantSymbol" = '0308'
WHERE "CountryId" = (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1)
  AND "ConstantSymbol" IS NULL;

-- The ONE legal notice this platform is entitled to print. The sentence is verbatim from the real
-- Czech invoice the owner issues, so its provenance is the business itself (LegalDisclaimerReviewStatus
-- = 1, BusinessSupplied) — not a lawyer, which is why it is 1 and not 2 (CounselReviewed).
--
-- Every other country is left NotReviewed ON PURPOSE. The rows that used to sit here asserted things
-- about German, Austrian, Polish, Slovak, US, UK, French, Italian and Spanish law that nobody with
-- standing ever checked, and one asserted Czech law in English under a Czech heading. A notice
-- reviewed for one jurisdiction is not a notice for another and a translation of it is not the same
-- artifact, so these stay empty until the owner's lawyer supplies each one, and the invoice prints the
-- generic English fallback in the meantime.
UPDATE public."CountryInvoiceConfigs" SET
  "LegalDisclaimerTemplate" = 'Dovolujeme si Vás upozornit, že v případě nedodržení data splatnosti uvedeného na faktuře Vám můžeme účtovat zákonný úrok z prodlení.',
  "LegalDisclaimerLanguageCode" = 'cs',
  "LegalDisclaimerReviewStatus" = 1
WHERE "CountryId" = (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1)
  AND "LegalDisclaimerReviewStatus" = 0;

-- ============================================================
-- COUNTRY CONFIGURATIONS
-- ============================================================
-- "InsuranceCoverageAmount" is the per-booking insurance ceiling customer copy states, a number in the
-- row's DefaultCurrencyCode. Every row is NULL -- the clients render the no-figure copy variant -- until
-- the owner decides whose policy covers a booking and at what figure (owner ruling 2026-09-28), and
-- authors it on the admin country form.
-- "IsDefaultMarket" is what a customer surface pre-selects before any choice is made (Q-MARKET-01):
-- exactly one row carries it (partial unique index), CZE today; SetDefaultMarket moves it.
-- "OperatorTenantId" is the operating company that serves the market (ADR-0061 D2). Only CZE has one;
-- a country whose row is NULL here is not listed by GetMarkets and refuses anonymous writes with
-- tenant.not_found until a company is assigned to it.
INSERT INTO public."CountryConfigurations" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
  "CountryId", "DefaultCurrencyCode", "DefaultLanguageCode",
  "DateFormat", "TimeZoneId", "PhonePrefix",
  "StandardVatRate",
  "TaxIdLabel", "TaxIdFormat",
  "RegistrationNumberLabel", "RegistrationNumberFormat", "RegistrationNumberRequired",
  "VatNumberLabel", "VatNumberFormat", "VatNumberRequired",
  "DefaultPaymentGateway", "PayoutScheme",
  "InsuranceCoverageAmount", "IsDefaultMarket", "OperatorTenantId"
)
VALUES
  -- Czech Republic — IČO (company ID) mandatory, DIČ (VAT ID) optional
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1),
   'CZK', 'cs', 'dd.MM.yyyy', 'Europe/Prague', '+420',
   0.21, 'IČO', '^\d{8}$',
   'IČO', '^\d{8}$', true,
   'DIČ', '^CZ\d{8,10}$', false,
   'Stripe', 1,
   NULL, true, 'cleansia-cz')
ON CONFLICT ("CountryId") DO NOTHING;

-- ============================================================
-- EMPLOYEE DOCUMENT REQUIREMENTS
-- ============================================================
-- Which document types a cleaner must supply before an admin may approve them, per country.
-- ApproveEmployee reads these rows: a required type that is missing, or present but not Approved,
-- blocks approval. A country with NO rows gates nothing, which is how every unseeded market behaves.
--
-- Deliberately short, and deliberately NOT a statement about Czech or Slovak employment law. The
-- rows below are the two this platform can justify on its own account: it must know who it is
-- engaging, and it must be able to see a work permit from someone who needs one. Everything else a
-- jurisdiction may demand is left for the admin screen to add, which is the entire reason these are
-- rows rather than a constant.
--
-- InsuranceDocument is required (owner ruling 2026-09-28): a cleaner is approved only with a
-- valid liability insurance certificate, whichever policy the platform finally promises.
--
-- WorkPermit is seeded NOT required on purpose. It applies to non-EU nationals and to nobody else,
-- and a per-country flag cannot say "required for some of these people" — so it appears on the
-- cleaner's checklist as expected-but-optional and an admin judges the individual case.
--
-- Idempotent: (CountryId, DocumentType) is unique, so re-running this leaves existing rows alone
-- rather than duplicating them.
INSERT INTO public."EmployeeDocumentRequirements" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
  "CountryId", "DocumentType", "IsRequired", "SortOrder"
)
SELECT
  generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
  c."Id", r.document_type, r.is_required, r.sort_order
FROM public."Countries" c
CROSS JOIN (VALUES
  -- 1 = IdentityCard, 4 = WorkPermit, 9 = InsuranceDocument (Cleansia.Core.Domain.Enums.DocumentType)
  (1, true, 1),
  (4, false, 2),
  (9, true, 3)
) AS r(document_type, is_required, sort_order)
WHERE c."IsoCode" = 'CZE'
ON CONFLICT ("CountryId", "DocumentType") DO NOTHING;

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
(generate_ulid()::TEXT, true, 5, 'FooterText', '© 2026 Cleansia. Все права защищены.', (SELECT "Id" FROM public."Languages" WHERE "Code" = 'ru'), 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL)
ON CONFLICT ("EmailType", "LanguageId", "Key") DO NOTHING;

-- ============================================================
-- LOYALTY TIER CONFIGS
-- ============================================================
-- The console edits a tier (UpdateTierConfig) and cannot create one, and with no row the resolver
-- applies no tier discount at all, so the four rows are made here. The brand's programme, sold
-- identically by every operating company: no tenant (ADR-0061 D7).
-- DiscountPercent stored as a fraction in [0, 1] (e.g. 0.05 = 5%).
-- One guarded INSERT per tier: the customer app's loyalty-tier-claim.spec.ts reads each PerksJson
-- together with the "Tier" its guard names, to prove every seeded perk has copy in five locales.
INSERT INTO public."LoyaltyTierConfigs" (
    "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
    "DeactivatedBy", "DeactivatedOn",
    "Tier", "LifetimePointsThreshold", "DiscountPercent",
    "MinimumOrderAmountForDiscount", "PerksJson"
)
SELECT '01LTYBRONZE000000000000000', true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
    1, 0, 0.0000, NULL,
    '[{"icon":"badge","labelKey":"loyalty.perks.welcome_badge"}]'
WHERE NOT EXISTS (SELECT 1 FROM public."LoyaltyTierConfigs" WHERE "Tier" = 1);

INSERT INTO public."LoyaltyTierConfigs" (
    "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
    "DeactivatedBy", "DeactivatedOn",
    "Tier", "LifetimePointsThreshold", "DiscountPercent",
    "MinimumOrderAmountForDiscount", "PerksJson"
)
SELECT '01LTYSILVER000000000000000', true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
    2, 500, 0.0500, 1000.00,
    '[{"icon":"badge","labelKey":"loyalty.perks.welcome_badge"},{"icon":"percent","labelKey":"loyalty.perks.discount_5_above_1000"}]'
WHERE NOT EXISTS (SELECT 1 FROM public."LoyaltyTierConfigs" WHERE "Tier" = 2);

INSERT INTO public."LoyaltyTierConfigs" (
    "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
    "DeactivatedBy", "DeactivatedOn",
    "Tier", "LifetimePointsThreshold", "DiscountPercent",
    "MinimumOrderAmountForDiscount", "PerksJson"
)
SELECT '01LTYGOLD0000000000000000A', true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
    3, 2000, 0.1000, 1000.00,
    '[{"icon":"badge","labelKey":"loyalty.perks.welcome_badge"},{"icon":"percent","labelKey":"loyalty.perks.discount_10"}]'
WHERE NOT EXISTS (SELECT 1 FROM public."LoyaltyTierConfigs" WHERE "Tier" = 3);

INSERT INTO public."LoyaltyTierConfigs" (
    "Id", "IsActive", "CreatedBy", "CreatedOn", "UpdatedBy", "UpdatedOn",
    "DeactivatedBy", "DeactivatedOn",
    "Tier", "LifetimePointsThreshold", "DiscountPercent",
    "MinimumOrderAmountForDiscount", "PerksJson"
)
SELECT '01LTYPLATINUM0000000000000', true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
    4, 5000, 0.1200, 1000.00,
    '[{"icon":"badge","labelKey":"loyalty.perks.welcome_badge"},{"icon":"percent","labelKey":"loyalty.perks.discount_12"}]'
WHERE NOT EXISTS (SELECT 1 FROM public."LoyaltyTierConfigs" WHERE "Tier" = 4);

-- ============================================================================
-- PropertySizePresets — the per-country size ladder.
--
-- The label is the only country-specific part: an order stores Rooms and
-- Bathrooms as integers and OrderPricingCalculator prices on those, so nothing
-- persists "3+kk" and a new market is a new list over the same two numbers.
-- Because the order never references a preset, retiring one cannot make a
-- historic order unpriceable.  -> /decisions/adr-0056
-- Codes carry their market so they stay unique and readable in an admin list.
-- ============================================================================

INSERT INTO public."PropertySizePresets" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
  "CountryId", "Code", "SortOrder", "Rooms", "Bathrooms", "Translations"
)
SELECT generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
       c."Id", c."IsoCode" || '_' || p.code, p.sort_order, p.rooms, p.bathrooms, p.translations::jsonb
FROM public."Countries" c
CROSS JOIN (VALUES
  ('1KK',   1, 1, 1, '{"en":{"Name":"1 room","Description":""},"cs":{"Name":"1+kk","Description":""},"sk":{"Name":"1+kk","Description":""},"uk":{"Name":"1 кімната","Description":""},"ru":{"Name":"1 комната","Description":""}}'),
  ('2KK',   2, 2, 1, '{"en":{"Name":"2 rooms","Description":""},"cs":{"Name":"2+kk","Description":""},"sk":{"Name":"2+kk","Description":""},"uk":{"Name":"2 кімнати","Description":""},"ru":{"Name":"2 комнаты","Description":""}}'),
  ('3KK',   3, 3, 1, '{"en":{"Name":"3 rooms","Description":""},"cs":{"Name":"3+kk","Description":""},"sk":{"Name":"3+kk","Description":""},"uk":{"Name":"3 кімнати","Description":""},"ru":{"Name":"3 комнаты","Description":""}}'),
  ('4KK',   4, 4, 2, '{"en":{"Name":"4 rooms","Description":""},"cs":{"Name":"4+kk","Description":""},"sk":{"Name":"4+kk","Description":""},"uk":{"Name":"4 кімнати","Description":""},"ru":{"Name":"4 комнаты","Description":""}}'),
  ('HOUSE', 5, 5, 2, '{"en":{"Name":"House","Description":""},"cs":{"Name":"Dům","Description":""},"sk":{"Name":"Dom","Description":""},"uk":{"Name":"Будинок","Description":""},"ru":{"Name":"Дом","Description":""}}')
) AS p(code, sort_order, rooms, bathrooms, translations)
WHERE c."IsoCode" = 'CZE'
ON CONFLICT ("CountryId", "Code") DO NOTHING;

COMMIT;
