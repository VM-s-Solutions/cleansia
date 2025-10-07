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
