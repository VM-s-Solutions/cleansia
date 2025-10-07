-- INSERT EMPLOYEE PAYROLL DATA
-- This script inserts seed data for PayPeriods, EmployeePayConfig, OrderEmployeePay, and EmployeeInvoices

-- 1. PAY PERIODS
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

-- 2. EMPLOYEE PAY CONFIG
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

-- 3. ORDER EMPLOYEE PAY
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

-- 4. EMPLOYEE INVOICES
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
