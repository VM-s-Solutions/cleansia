-- ================================================
-- Data Migration: Set Default Language for Existing Users
-- ================================================
-- Purpose: Update existing users with default language preferences
--          Czech for @cleansia.cz emails, English for all others
-- ================================================

-- Set Czech language for employees with @cleansia.cz emails
UPDATE public."Users"
SET "PreferredLanguageCode" = 'cs'
WHERE "Email" LIKE '%@cleansia.cz'
  AND "PreferredLanguageCode" IS NULL;

-- Set English language for all other users without preference
UPDATE public."Users"
SET "PreferredLanguageCode" = 'en'
WHERE "PreferredLanguageCode" IS NULL;

-- Verify the migration results
SELECT
    "PreferredLanguageCode",
    COUNT(*) as UserCount
FROM public."Users"
GROUP BY "PreferredLanguageCode"
ORDER BY "PreferredLanguageCode";
