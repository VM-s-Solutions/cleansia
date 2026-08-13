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
