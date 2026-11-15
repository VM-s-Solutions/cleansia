-- DEV ONLY, read-only. Lists the fee receipts of the end-to-end test orders (customer e-mail e2e.<run>.<role>@example.com)
-- issued in the last 14 days, for the E2E harness's PAY-042 (T-0934, owner ruling 2026-11-15): no endpoint lists a
-- fee receipt (the order's download serves the sale receipt only) and none is e-mailed, so its row is read here.
-- A fee receipt is an OrderReceipts row that names the receivable it was paid for (OrderReceipt.IsFee).
-- Changes nothing: one read-only transaction. Refuses to run on a database that does not hold the published DEV
-- administrator, which PRO never does.
BEGIN TRANSACTION READ ONLY;

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM "Users" WHERE "Email" = 'admin@cleansia.local') THEN
    RAISE EXCEPTION 'dev-e2e-read-fee-receipts.sql is DEV only: the published DEV administrator is missing';
  END IF;
END $$;

SELECT o."Id" AS order_id, o."CustomerEmail" AS customer, r."ReceivableId" AS receivable_id, rv."Amount" AS fee_amount,
       rv."Status" AS receivable_status, r."ReceiptNumber" AS receipt_number, r."IssuedAt" AS issued_at, r."FileName" AS file_name,
       r."EmailSent" AS email_sent, r."FiscalRegistrationFailed" AS fiscal_failed
FROM "OrderReceipts" r
JOIN "Orders" o ON o."Id" = r."OrderId"
JOIN "Receivables" rv ON rv."Id" = r."ReceivableId"
WHERE r."ReceivableId" IS NOT NULL
  AND o."CustomerEmail" LIKE 'e2e.%@example.com'
  AND r."IssuedAt" > now() - interval '14 days'
ORDER BY r."IssuedAt" DESC;

COMMIT;
