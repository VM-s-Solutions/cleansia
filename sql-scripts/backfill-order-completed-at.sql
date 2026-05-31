-- backfill-order-completed-at.sql
--
-- One-off backfill for the new `Orders.CompletedAt` column added in
-- the `AddOrderCompletedAt` EF migration. Run AFTER the migration
-- has been applied; safe to re-run (idempotent thanks to the
-- `CompletedAt IS NULL` guard).
--
-- For every Completed order that doesn't yet have a CompletedAt
-- timestamp, derive it from the OrderStatusHistory entry where the
-- status flipped to Completed (status int value = 5 — see
-- Cleansia.Core.Domain.Enums.OrderStatus). The MIN guards against
-- the (rare but possible) case of multiple Completed entries in
-- history; the earliest one is the actual completion event.
--
-- Why OrderStatusHistory and not OrderEmployeePay.CreatedOn:
--   - Pay rows can lag the completion event (background payroll job).
--   - Pay rows can be missing for non-payroll completions / legacy
--     data / partial test fixtures.
--   - OrderStatusHistory.CreatedOn is the audit timestamp of the
--     status mutation itself — closest possible proxy for "when did
--     the order actually finish".

UPDATE "Orders" o
SET "CompletedAt" = sub.completed_at
FROM (
    SELECT
        osh."OrderId" AS order_id,
        MIN(osh."CreatedOn") AT TIME ZONE 'UTC' AS completed_at
    FROM "OrderStatusHistory" osh
    WHERE osh."Status" = 5  -- OrderStatus.Completed
    GROUP BY osh."OrderId"
) sub
WHERE o."Id" = sub.order_id
  AND o."CompletedAt" IS NULL;

-- Sanity output: how many orders we just touched + how many
-- Completed orders are still missing a timestamp (should be 0).
SELECT
    (SELECT COUNT(*) FROM "Orders" WHERE "CompletedAt" IS NOT NULL) AS "OrdersWithCompletedAt",
    (
        SELECT COUNT(*)
        FROM "Orders" o
        WHERE o."CompletedAt" IS NULL
          AND EXISTS (
              SELECT 1 FROM "OrderStatusHistory" osh
              WHERE osh."OrderId" = o."Id" AND osh."Status" = 5
          )
    ) AS "CompletedOrdersStillMissingTimestamp";
