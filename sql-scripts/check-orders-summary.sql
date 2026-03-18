-- Orders Summary Report
-- Overview of orders by status, recent activity

-- 1. Orders by status
SELECT
    "Status",
    COUNT(*) AS order_count,
    COALESCE(SUM("TotalPrice"), 0) AS total_revenue
FROM public."Orders"
WHERE "IsActive" = true
GROUP BY "Status"
ORDER BY order_count DESC;

-- 2. Orders in last 30 days
SELECT
    DATE("CreatedOn") AS order_date,
    COUNT(*) AS orders,
    COALESCE(SUM("TotalPrice"), 0) AS revenue
FROM public."Orders"
WHERE "CreatedOn" >= CURRENT_DATE - INTERVAL '30 days'
  AND "IsActive" = true
GROUP BY DATE("CreatedOn")
ORDER BY order_date DESC;

-- 3. Recent orders with details
SELECT
    o."Id",
    u."Email" AS customer_email,
    u."FirstName" || ' ' || u."LastName" AS customer_name,
    o."Status",
    o."TotalPrice",
    o."CreatedOn"
FROM public."Orders" o
JOIN public."Users" u ON o."UserId" = u."Id"
WHERE o."IsActive" = true
ORDER BY o."CreatedOn" DESC
LIMIT 20;
