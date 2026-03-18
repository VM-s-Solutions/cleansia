-- Disputes Summary Report

-- 1. Disputes by status
SELECT
    "Status",
    COUNT(*) AS dispute_count
FROM public."Disputes"
WHERE "IsActive" = true
GROUP BY "Status"
ORDER BY dispute_count DESC;

-- 2. Recent disputes
SELECT
    d."Id",
    u."Email" AS customer_email,
    u."FirstName" || ' ' || u."LastName" AS customer_name,
    d."Status",
    d."Reason",
    d."CreatedOn"
FROM public."Disputes" d
JOIN public."Orders" o ON d."OrderId" = o."Id"
JOIN public."Users" u ON o."UserId" = u."Id"
WHERE d."IsActive" = true
ORDER BY d."CreatedOn" DESC
LIMIT 20;
