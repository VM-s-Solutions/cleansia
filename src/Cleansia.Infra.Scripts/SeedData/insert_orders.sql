-- INSERT PACKAGE SERVICES (Junction table for packages and services)
INSERT INTO public."PackageServices" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "PackageId", "ServiceId"
)
VALUES
  -- Essential Clean Package Services
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Essential Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'General Cleaning' LIMIT 1)),
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Essential Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Bathroom Cleaning' LIMIT 1)),

  -- Complete Home Clean Package Services
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Complete Home Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'General Cleaning' LIMIT 1)),
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Complete Home Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Bathroom Cleaning' LIMIT 1)),
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Complete Home Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Window Cleaning' LIMIT 1)),

  -- Deep Clean Premium Package Services
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Deep Clean Premium' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Deep Cleaning' LIMIT 1)),
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Deep Clean Premium' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Kitchen Deep Clean' LIMIT 1)),
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Deep Clean Premium' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Bathroom Cleaning' LIMIT 1)),

  -- Kitchen & Bathroom Focus Package Services
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Kitchen & Bathroom Focus' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Kitchen Deep Clean' LIMIT 1)),
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Kitchen & Bathroom Focus' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Bathroom Cleaning' LIMIT 1)),

  -- Eco-Green Package Services
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Eco-Green Package' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Eco-Friendly Cleaning' LIMIT 1)),
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Eco-Green Package' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'General Cleaning' LIMIT 1)),

  -- Moving Day Special Package Services
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Moving Day Special' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Move-in/Move-out Cleaning' LIMIT 1)),
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Moving Day Special' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Deep Cleaning' LIMIT 1)),

  -- Post-Renovation Clean Package Services
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Post-Renovation Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Post-Construction Cleanup' LIMIT 1)),
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Post-Renovation Clean' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Deep Cleaning' LIMIT 1)),

  -- Luxury Full Service Package Services
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Luxury Full Service' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Deep Cleaning' LIMIT 1)),
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Luxury Full Service' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Window Cleaning' LIMIT 1)),
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Luxury Full Service' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Carpet Cleaning' LIMIT 1)),
  ((SELECT "Id" FROM public."Packages" WHERE "Name" = 'Luxury Full Service' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Upholstery Cleaning' LIMIT 1));

-- INSERT ORDERS
INSERT INTO public."Orders" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "CustomerName", "CustomerEmail",
  "CustomerPhone", "CustomerAddressId", "DisplayOrderNumber",
  "Rooms", "Bathrooms", "CleaningDateTime", "PaymentType",
  "PaymentStatus", "TotalPrice", "EstimatedTime",
  "ConfirmationCode", "StripeSessionId", "Notes",
  "SpecialInstructions", "AccessInstructions", "SelectedPackageId",
  "CurrencyId", "UserId", "EmployeeId", "Extras"
)
VALUES
  -- Order 1: Jan Novák - Essential Clean
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Jan Novák', 'jan.novak@email.cz', '+420123456789',
   (SELECT "Id" FROM public."Addresses" WHERE "Street" = 'Wenceslas Square 1' LIMIT 1),
   'CLS-2025-0001', 3, 2, '2025-01-15 10:00:00', 1, 1, 1299.00, 180,
   'ABC123XYZ', 'cs_test_stripe_session_1',
   'Regular maintenance cleaning for family apartment',
   'Please be careful with the antique furniture in the living room',
   'Key under the mat, ring bell twice',
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Essential Clean' LIMIT 1),
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'jan.novak@email.cz' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654321' LIMIT 1),
   '{"eco_products": true, "pet_friendly": false, "extra_vacuum": true}'),

  -- Order 2: Marie Svobodová - Deep Clean Premium
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Marie Svobodová', 'marie.svobodova@email.cz', '+420234567890',
   (SELECT "Id" FROM public."Addresses" WHERE "Street" = 'Národní třída 25' LIMIT 1),
   'CLS-2025-0002', 4, 3, '2025-01-16 09:00:00', 0, 0, 2199.00, 240,
   'DEF456ABC', 'cs_test_stripe_session_2',
   'Deep cleaning after renovation work',
   'There was recent painting work, please be extra careful with dust removal',
   'Security code: 1234, apartment 3B',
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Deep Clean Premium' LIMIT 1),
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'marie.svobodova@email.cz' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654322' LIMIT 1),
   '{"eco_products": false, "pet_friendly": true, "extra_vacuum": false}'),

  -- Order 3: Petr Dvořák - Moving Day Special
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Petr Dvořák', 'petr.dvorak@email.cz', '+420345678901',
   (SELECT "Id" FROM public."Addresses" WHERE "Street" = 'Vinohrady 456' LIMIT 1),
   'CLS-2025-0003', 2, 1, '2025-01-17 14:00:00', 1, 1, 2799.00, 300,
   'GHI789DEF', 'cs_test_stripe_session_3',
   'Move-out cleaning for apartment rental',
   'Need to return security deposit, please ensure everything is spotless',
   'Landlord will be present for inspection',
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Moving Day Special' LIMIT 1),
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'petr.dvorak@email.cz' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654323' LIMIT 1),
   '{"eco_products": true, "pet_friendly": false, "extra_vacuum": true}'),

  -- Order 4: Anna Černá - Kitchen & Bathroom Focus
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Anna Černá', 'anna.cerna@email.cz', '+420456789012',
   (SELECT "Id" FROM public."Addresses" WHERE "Street" = 'Karlínské náměstí 12' LIMIT 1),
   'CLS-2025-0004', 3, 2, '2025-01-18 11:00:00', 0, 0, 1399.00, 150,
   'JKL012GHI', 'cs_test_stripe_session_4',
   'Focus on kitchen and bathrooms only, other rooms are fine',
   'Kitchen has stubborn grease stains from cooking',
   'Use main entrance, elevator to 4th floor',
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Kitchen & Bathroom Focus' LIMIT 1),
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'anna.cerna@email.cz' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654324' LIMIT 1),
   '{"eco_products": false, "pet_friendly": true, "extra_vacuum": false}'),

  -- Order 5: Tomáš Procházka - Eco-Green Package
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Tomáš Procházka', 'tomas.prochazka@email.cz', '+420567890123',
   (SELECT "Id" FROM public."Addresses" WHERE "Street" = 'Smíchov 789' LIMIT 1),
   'CLS-2025-0005', 5, 3, '2025-01-19 08:00:00', 1, 1, 1899.00, 210,
   'MNO345JKL', 'cs_test_stripe_session_5',
   'Eco-friendly cleaning for family with small children',
   'Please use only non-toxic products due to allergies',
   'Doorbell broken, please call when arriving',
   (SELECT "Id" FROM public."Packages" WHERE "Name" = 'Eco-Green Package' LIMIT 1),
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'tomas.prochazka@email.cz' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654325' LIMIT 1),
   '{"eco_products": true, "pet_friendly": true, "extra_vacuum": true}'),

  -- Order 6: Complete Home Clean (No package, individual services)
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   'Jan Novák', 'jan.novak@email.cz', '+420123456789',
   (SELECT "Id" FROM public."Addresses" WHERE "Street" = 'Wenceslas Square 1' LIMIT 1),
   'CLS-2025-0006', 3, 2, '2025-01-20 13:00:00', 0, 0, 1150.00, 165,
   'PQR678MNO', NULL,
   'Follow-up cleaning with individual services',
   'Focus on areas missed in previous cleaning',
   'Same access as before',
   NULL,
   (SELECT "Id" FROM public."Currencies" WHERE "Code" = 'CZK' LIMIT 1),
   (SELECT "Id" FROM public."Users" WHERE "Email" = 'jan.novak@email.cz' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "ICO" = '87654321' LIMIT 1),
   '{"eco_products": false, "pet_friendly": false, "extra_vacuum": false}');

-- INSERT ORDER SERVICES (Junction table for orders and individual services)
INSERT INTO public."OrderServices" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "OrderId", "ServiceId", "Quantity", "Price"
)
VALUES
  -- Additional services for Order 6 (no package)
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0006' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'General Cleaning' LIMIT 1),
   1, 650.00),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0006' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Window Cleaning' LIMIT 1),
   1, 250.00),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0006' LIMIT 1),
   (SELECT "Id" FROM public."Services" WHERE "Name" = 'Carpet Cleaning' LIMIT 1),
   1, 250.00);

-- INSERT ORDER STATUS TRACKS (Order history)
INSERT INTO public."OrderStatusTracks" (
  "Id", "IsActive", "CreatedBy", "CreatedOn",
  "UpdatedBy", "UpdatedOn", "DeactivatedBy",
  "DeactivatedOn", "Status", "ChangedAt", "ChangedBy",
  "Notes", "OrderId"
)
VALUES
  -- Order 1 Status History
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   0, CURRENT_TIMESTAMP - INTERVAL '3 days', 'system',
   'Order placed and confirmed',
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0001' LIMIT 1)),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   1, CURRENT_TIMESTAMP - INTERVAL '2 days', 'katerina.novotna@cleansia.cz',
   'Employee assigned and scheduled',
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0001' LIMIT 1)),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   2, CURRENT_TIMESTAMP - INTERVAL '1 day', 'katerina.novotna@cleansia.cz',
   'Cleaning completed successfully',
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0001' LIMIT 1)),

  -- Order 2 Status History
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   0, CURRENT_TIMESTAMP - INTERVAL '2 days', 'system',
   'Order placed, pending payment',
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0002' LIMIT 1)),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   1, CURRENT_TIMESTAMP - INTERVAL '1 day', 'michal.krejci@cleansia.cz',
   'Employee assigned, awaiting cleaning date',
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0002' LIMIT 1)),

  -- Order 3 Status History
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   0, CURRENT_TIMESTAMP - INTERVAL '1 day', 'system',
   'Order placed and confirmed',
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0003' LIMIT 1)),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   1, CURRENT_TIMESTAMP - INTERVAL '6 hours', 'zuzana.horakova@cleansia.cz',
   'Employee assigned for move-out cleaning',
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0003' LIMIT 1)),

  -- Order 4 Status History
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   0, CURRENT_TIMESTAMP - INTERVAL '12 hours', 'system',
   'Order placed, pending payment confirmation',
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0004' LIMIT 1)),

  -- Order 5 Status History
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   0, CURRENT_TIMESTAMP - INTERVAL '6 hours', 'system',
   'Order placed and confirmed',
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0005' LIMIT 1)),

  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   1, CURRENT_TIMESTAMP - INTERVAL '3 hours', 'lenka.markova@cleansia.cz',
   'Employee assigned for eco-friendly cleaning',
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0005' LIMIT 1)),

  -- Order 6 Status History
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP, NULL, NULL, NULL, NULL,
   0, CURRENT_TIMESTAMP - INTERVAL '2 hours', 'system',
   'Follow-up order placed, pending payment',
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0006' LIMIT 1));