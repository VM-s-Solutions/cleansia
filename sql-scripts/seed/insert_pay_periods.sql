-- INSERT PAY PERIODS
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
