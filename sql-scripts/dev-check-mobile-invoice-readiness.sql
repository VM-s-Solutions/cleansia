BEGIN TRANSACTION READ ONLY;
SET LOCAL statement_timeout = '5s';
SELECT i."InvoiceNumber",
       i."PdfGenerationFailed",
       NULLIF(BTRIM(i."PdfBlobUrl"), '') IS NOT NULL AS "HasPdfPath",
       CASE WHEN NULLIF(BTRIM(i."PdfBlobUrl"), '') IS NULL THEN 'absent'
            WHEN i."PdfBlobUrl" LIKE 'https://%' THEN 'absolute'
            ELSE 'relative' END AS "PdfPathShape",
       i."CreatedOn", i."UpdatedOn"
FROM "EmployeeInvoices" i
WHERE i."TenantId" = 'cleansia-cz'
  AND i."InvoiceNumber" = 'INV-2026-000001';
ROLLBACK;
