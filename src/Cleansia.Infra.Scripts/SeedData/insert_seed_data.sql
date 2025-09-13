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

-- 3. EMAIL TRANSLATIONS
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
