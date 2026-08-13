-- Fix Employee Address Connections
-- This script creates addresses for employees and connects them properly

BEGIN TRANSACTION;

-- 1. First, create addresses for employees (they currently don't have their own addresses)
INSERT INTO public."Addresses" (
    "Id", "IsActive", "CreatedBy", "CreatedOn",
    "UpdatedBy", "UpdatedOn", "DeactivatedBy", "DeactivatedOn",
    "Street", "City", "ZipCode", "CountryId"
)
VALUES
    -- Employee Address 1: Kateřina Novotná (Employee residential address)
    (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
     'Vinohrady 15', 'Prague 2', '12000',
     (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1)),

    -- Employee Address 2: Michal Krejčí (Employee residential address)
    (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
     'Karlín 28', 'Prague 8', '18600',
     (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1)),

    -- Employee Address 3: Zuzana Horáková (Employee residential address)
    (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
     'Vinohrady 42', 'Prague 2', '12000',
     (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1)),

    -- Employee Address 4: Tomáš Dvořák (Employee residential address)
    (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
     'Vršovice 73', 'Prague 10', '10000',
     (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1)),

    -- Employee Address 5: Petra Svobodan (Employee residential address)
    (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
     'Smíchov 156', 'Prague 5', '15000',
     (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1)),

    -- Employee Address 6: Jan Procházka (Employee residential address)
    (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
     'Karlín 89', 'Prague 8', '18600',
     (SELECT "Id" FROM public."Countries" WHERE "IsoCode" = 'CZE' LIMIT 1));

-- 2. Update employees to link them with their respective addresses
UPDATE public."Employees" 
SET "AddressId" = (
    CASE 
        -- Link Kateřina Novotná with her address
        WHEN "UserId" = (SELECT "Id" FROM public."Users" WHERE "Email" = 'katerina.novotna@cleansia.cz' LIMIT 1) 
        THEN (SELECT "Id" FROM public."Addresses" WHERE "Street" = 'Vinohrady 15' AND "City" = 'Prague 2' LIMIT 1)
        
        -- Link Michal Krejčí with his address
        WHEN "UserId" = (SELECT "Id" FROM public."Users" WHERE "Email" = 'michal.krejci@cleansia.cz' LIMIT 1) 
        THEN (SELECT "Id" FROM public."Addresses" WHERE "Street" = 'Karlín 28' AND "City" = 'Prague 8' LIMIT 1)
        
        -- Link Zuzana Horáková with her address
        WHEN "UserId" = (SELECT "Id" FROM public."Users" WHERE "Email" = 'zuzana.horakova@cleansia.cz' LIMIT 1) 
        THEN (SELECT "Id" FROM public."Addresses" WHERE "Street" = 'Vinohrady 42' AND "City" = 'Prague 2' LIMIT 1)
        
        -- Link Tomáš Dvořák with his address
        WHEN "UserId" = (SELECT "Id" FROM public."Users" WHERE "Email" = 'tomas.dvorak@cleansia.cz' LIMIT 1) 
        THEN (SELECT "Id" FROM public."Addresses" WHERE "Street" = 'Vršovice 73' AND "City" = 'Prague 10' LIMIT 1)
        
        -- Link Petra Svobodan with her address
        WHEN "UserId" = (SELECT "Id" FROM public."Users" WHERE "Email" = 'petra.svobodan@cleansia.cz' LIMIT 1) 
        THEN (SELECT "Id" FROM public."Addresses" WHERE "Street" = 'Smíchov 156' AND "City" = 'Prague 5' LIMIT 1)
        
        -- Link Jan Procházka with his address
        WHEN "UserId" = (SELECT "Id" FROM public."Users" WHERE "Email" = 'jan.prochazka@cleansia.cz' LIMIT 1) 
        THEN (SELECT "Id" FROM public."Addresses" WHERE "Street" = 'Karlín 89' AND "City" = 'Prague 8' LIMIT 1)
        
        ELSE "AddressId" -- Keep existing AddressId if no match
    END
)
WHERE "UserId" IN (
    SELECT "Id" FROM public."Users" 
    WHERE "Email" IN (
        'katerina.novotna@cleansia.cz',
        'michal.krejci@cleansia.cz', 
        'zuzana.horakova@cleansia.cz',
        'tomas.dvorak@cleansia.cz',
        'petra.svobodan@cleansia.cz',
        'jan.prochazka@cleansia.cz'
    )
);

-- 3. Verify the connections
SELECT 
    u."Email",
    u."FirstName" || ' ' || u."LastName" AS "FullName",
    e."Id" AS "EmployeeId",
    e."AddressId",
    a."Street",
    a."City",
    a."ZipCode",
    c."Name" AS "Country"
FROM public."Employees" e
JOIN public."Users" u ON e."UserId" = u."Id"
LEFT JOIN public."Addresses" a ON e."AddressId" = a."Id"
LEFT JOIN public."Countries" c ON a."CountryId" = c."Id"
ORDER BY u."Email";

COMMIT TRANSACTION;