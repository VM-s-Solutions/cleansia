-- ============================================================================
-- READ-ONLY. Counts only. Safe against PRO.
--
-- What this answers, and why it is asked BEFORE the retention sweep is switched on
-- (T-0685):
--
-- The sweep's CleanOrderCustomerPii task selects completed orders whose CleaningDateTime
-- is older than the retention window (default 2 years, RetentionDefaults.DefaultOrderPiiYears)
-- and calls order.CustomerAddress?.Anonymize() on each. Address rows are DELIBERATELY
-- deduplicated across users on (Street, City, ZipCode, CountryId) — AddressRepository
-- .GetAddressAsync — so one row is shared by everyone in a building. Anonymize() overwrites
-- Street/City/ZipCode/State in place with '[DELETED]'.
--
-- So a two-year-old order can blank an address that something LIVE still points at. Section 3
-- below is the number that matters. It checks all three tables that hold a foreign key into
-- Addresses — Orders, SavedAddresses and Employees — because counting only saved addresses
-- would report a false zero: the likeliest sharing case is two ORDERS at the same flat, one
-- past the window and one recent, since repeat bookings reuse the same deduped Address row.
--
-- Expected answer on a pre-release database: zero everywhere. If ANY row of section 3 is
-- non-zero, do not enable the sweep until the shared-address defect is fixed.
--
-- Enum note: OrderStatus.Completed = 5 (Cleansia.Core.Domain.Enums.OrderStatus).
-- ============================================================================

\echo '=== 1. Completed orders past the 2-year PII window (what the sweep would select) ==='

SELECT
    COUNT(*)                                        AS orders_selected,
    MIN(o."CleaningDateTime")                       AS oldest,
    MAX(o."CleaningDateTime")                       AS newest
FROM public."Orders" o
WHERE o."CleaningDateTime" < (NOW() - INTERVAL '2 years')
  AND o."CustomerName" IS DISTINCT FROM '[DELETED]'
  AND EXISTS (
      SELECT 1
      FROM public."OrderStatusHistory" h
      WHERE h."OrderId" = o."Id"
        AND h."Status" = 5
  );

\echo ''
\echo '=== 2. Distinct address rows those orders point at ==='

SELECT COUNT(DISTINCT o."CustomerAddressId") AS distinct_addresses_touched
FROM public."Orders" o
WHERE o."CleaningDateTime" < (NOW() - INTERVAL '2 years')
  AND o."CustomerName" IS DISTINCT FROM '[DELETED]'
  AND EXISTS (
      SELECT 1
      FROM public."OrderStatusHistory" h
      WHERE h."OrderId" = o."Id"
        AND h."Status" = 5
  );

\echo ''
\echo '=== 3. THE NUMBER THAT MATTERS: shared addresses something LIVE still depends on ==='
\echo '=== Non-zero on any row below means enabling the sweep blanks live data ==='

-- Addresses has exactly three dependants (FK_Orders_Addresses_CustomerAddressId,
-- FK_SavedAddresses_Addresses_AddressId, FK_Employees_Addresses_AddressId). All three are checked.
-- The order-to-order case is listed FIRST because it is the likeliest and the least obvious: repeat
-- bookings at one address reuse the same deduped Address row (OrderAddressResolver -> GetAddressAsync),
-- so a two-year-old order and last week's order at the same flat share one row. Counting only
-- SavedAddresses would report a false zero.
WITH doomed AS (
    SELECT DISTINCT o."CustomerAddressId" AS address_id
    FROM public."Orders" o
    WHERE o."CleaningDateTime" < (NOW() - INTERVAL '2 years')
      AND o."CustomerName" IS DISTINCT FROM '[DELETED]'
      AND EXISTS (
          SELECT 1
          FROM public."OrderStatusHistory" h
          WHERE h."OrderId" = o."Id"
            AND h."Status" = 5
      )
)
SELECT
    'in-window orders sharing a doomed address' AS dependant,
    COUNT(DISTINCT o."Id")                     AS rows_affected
FROM public."Orders" o
JOIN doomed d ON d.address_id = o."CustomerAddressId"
WHERE o."CleaningDateTime" >= (NOW() - INTERVAL '2 years')

UNION ALL

SELECT
    'live saved addresses on a doomed address',
    COUNT(DISTINCT sa."Id")
FROM public."SavedAddresses" sa
JOIN doomed d ON d.address_id = sa."AddressId"
WHERE sa."IsActive" = TRUE

UNION ALL

SELECT
    'active employees on a doomed address',
    COUNT(DISTINCT e."Id")
FROM public."Employees" e
JOIN doomed d ON d.address_id = e."AddressId"
WHERE e."IsActive" = TRUE;

\echo ''
\echo '=== 4. Context: total orders, and the feature-flag table this ticket is about ==='

SELECT
    (SELECT COUNT(*) FROM public."Orders")        AS total_orders,
    (SELECT COUNT(*) FROM public."FeatureFlags")  AS total_feature_flags,
    (SELECT COUNT(*) FROM public."FeatureFlags"
      WHERE "Name" = 'DataRetentionJobEnabled')   AS retention_flag_rows;
