-- DEV ONLY, read-only. Lists the Plus memberships DEV holds from the last 60 days, whether each was started on a phone
-- (its subscribe confirm recorded the membership) or through the web Checkout, its trial and period, and the Stripe
-- invoice and subscription events DEV processed in the last 14 days, so an invoice.payment_failed can be traced to
-- the subscription whose trial or period ended at that time. Prints no e-mail address, only whether the account is an
-- end-to-end test customer. Changes nothing: one read-only transaction. Refuses to run on a database that does not
-- hold the published DEV administrator, which PRO never does.
BEGIN TRANSACTION READ ONLY;

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "Users" WHERE "Email" = 'admin@cleansia.local') THEN
    RAISE EXCEPTION 'dev-e2e-read-plus-subscriptions.sql is DEV only: the published DEV administrator is missing';
  END IF;
END $$;

-- Status: 1 active, 2 past due, 3 cancelled, 4 paused.
SELECT m."Id" AS membership_id, m."UserId" AS user_id, (u."Email" LIKE 'e2e.%@example.com') AS e2e_customer,
       EXISTS (SELECT 1 FROM "CustomerActionAudits" a
               WHERE a."Action" = 'customer.membership.subscribe' AND a."ResourceId" = m."Id") AS started_on_phone,
       p."Code" AS plan, c."Code" AS currency, m."StripeSubscriptionId" AS subscription, m."Status" AS status,
       m."CreatedOn" AS created_on, m."TrialEndsAtUtc" AS trial_ends, m."CurrentPeriodStart" AS period_start,
       m."CurrentPeriodEnd" AS period_end, m."CancelledAt" AS cancelled_at, m."IsActive" AS active
FROM "UserMemberships" m
JOIN "Users" u ON u."Id" = m."UserId"
JOIN "MembershipPlans" p ON p."Id" = m."MembershipPlanId"
JOIN "Currencies" c ON c."Id" = m."CurrencyId"
WHERE m."CreatedOn" > now() - interval '60 days'
ORDER BY m."CreatedOn";

-- The invoice and subscription events DEV processed: their types and times.
SELECT e."EventType" AS event_type, e."StripeCreatedAt" AS stripe_created_at, e."ProcessedAt" AS processed_at
FROM "ProcessedStripeEvents" e
WHERE e."ProcessedAt" > now() - interval '14 days'
  AND (e."EventType" LIKE 'invoice.%' OR e."EventType" LIKE 'customer.subscription.%')
ORDER BY e."ProcessedAt";

COMMIT;
