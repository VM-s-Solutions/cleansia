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
   (SELECT "Id" FROM public."Employees" WHERE "RegistrationNumber" = '87654321' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2025-01-01 00:00:00' LIMIT 1),
   400.00, 260.00, 50.00, 100.00, 0.00, 810.00,
   '{"basePay": 400, "roomExtras": 160, "bathroomExtras": 100, "travelExpenses": 50, "qualityBonus": 100}',
   true),

  -- Order 2 (CLS-2025-0002) - Michal Krejčí - January period
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '19 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0002' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "RegistrationNumber" = '87654322' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2025-01-01 00:00:00' LIMIT 1),
   600.00, 500.00, 80.00, 150.00, 0.00, 1330.00,
   '{"basePay": 600, "roomExtras": 300, "bathroomExtras": 200, "travelExpenses": 80, "qualityBonus": 150}',
   true),

  -- Order 3 (CLS-2025-0003) - Zuzana Horáková - January period
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '18 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0003' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "RegistrationNumber" = '87654323' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2025-01-01 00:00:00' LIMIT 1),
   700.00, 290.00, 100.00, 200.00, 0.00, 1290.00,
   '{"basePay": 700, "roomExtras": 180, "bathroomExtras": 110, "travelExpenses": 100, "performanceBonus": 200}',
   true),

  -- Order 4 (CLS-2025-0004) - Pavel Veselý - January period
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '17 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0004' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "RegistrationNumber" = '87654324' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2025-01-01 00:00:00' LIMIT 1),
   450.00, 80.00, 40.00, 0.00, 50.00, 520.00,
   '{"basePay": 450, "bathroomExtras": 80, "travelExpenses": 40, "lateArrivalDeduction": -50}',
   true),

  -- Order 5 (CLS-2025-0005) - Lenka Marková - January period
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '16 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0005' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "RegistrationNumber" = '87654325' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2025-01-01 00:00:00' LIMIT 1),
   550.00, 510.00, 60.00, 120.00, 0.00, 1240.00,
   '{"basePay": 550, "roomExtras": 360, "bathroomExtras": 150, "travelExpenses": 60, "ecoProductBonus": 120}',
   true),

  -- Order 6 (CLS-2025-0006) - Kateřina Novotná - February period
  (generate_ulid()::TEXT, true, 'system', CURRENT_TIMESTAMP - INTERVAL '10 days', NULL, NULL, NULL, NULL,
   (SELECT "Id" FROM public."Orders" WHERE "DisplayOrderNumber" = 'CLS-2025-0006' LIMIT 1),
   (SELECT "Id" FROM public."Employees" WHERE "RegistrationNumber" = '87654321' LIMIT 1),
   (SELECT "Id" FROM public."PayPeriods" WHERE "StartDate" = '2025-02-01 00:00:00' LIMIT 1),
   350.00, 240.00, 45.00, 80.00, 0.00, 715.00,
   '{"basePay": 350, "roomExtras": 160, "bathroomExtras": 80, "travelExpenses": 45, "repeatClientBonus": 80}',
   true);
