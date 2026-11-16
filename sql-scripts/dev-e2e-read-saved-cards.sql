-- DEV ONLY, read-only. Lists the saved cards of the end-to-end test customers (e2e.<run>.<role>@example.com) made in
-- the last 7 days, their card orders, and the Stripe events DEV processed in the same hours, so a ticked card
-- payment that kept no card (E2E-PAY-022) can be traced to the step that did not happen. Changes nothing: one
-- read-only transaction. Refuses to run on a database that does not hold the published DEV administrator, which
-- PRO never does.
BEGIN TRANSACTION READ ONLY;

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "Users" WHERE "Email" = 'admin@cleansia.local') THEN
    RAISE EXCEPTION 'dev-e2e-read-saved-cards.sql is DEV only: the published DEV administrator is missing';
  END IF;
END $$;

-- Each saved card: captured or still waiting for Stripe, and whether a payment method was ever stored.
SELECT u."Email" AS customer, c."Id" AS saved_card_id, c."IsActive" AS active, c."CapturedOn" AS captured_on,
       (c."StripePaymentMethodId" IS NOT NULL) AS has_payment_method, c."CreatedOn" AS created_on
FROM "SavedCards" c
JOIN "Users" u ON u."Id" = c."UserId"
WHERE u."Email" LIKE 'e2e.%@example.com'
  AND c."CreatedOn" > now() - interval '7 days'
ORDER BY c."CreatedOn";

-- The card orders of the same customers: whether each was paid, and whether its PaymentIntent was recorded.
SELECT o."Id" AS order_id, o."CustomerEmail" AS customer, o."PaymentStatus" AS payment_status,
       (o."StripeSessionId" IS NOT NULL) AS has_session, (o."StripePaymentIntentId" IS NOT NULL) AS has_intent,
       o."CreatedOn" AS created_on
FROM "Orders" o
WHERE o."PaymentType" = 2
  AND o."CustomerEmail" LIKE 'e2e.%@example.com'
  AND o."CreatedOn" > now() - interval '7 days'
ORDER BY o."CreatedOn";

-- The Stripe events DEV processed while those cards were made: their types and times.
SELECT e."EventType" AS event_type, e."StripeCreatedAt" AS stripe_created_at, e."ProcessedAt" AS processed_at
FROM "ProcessedStripeEvents" e
WHERE e."ProcessedAt" > now() - interval '7 days'
ORDER BY e."ProcessedAt";

COMMIT;
