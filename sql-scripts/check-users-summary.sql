-- Users Summary Report
-- Overview of registered users, email confirmation status, and profiles

-- 1. Total users by role/profile
SELECT
    p."Name" AS profile_type,
    COUNT(*) AS user_count,
    SUM(CASE WHEN u."IsEmailConfirmed" = true THEN 1 ELSE 0 END) AS email_confirmed,
    SUM(CASE WHEN u."IsEmailConfirmed" = false THEN 1 ELSE 0 END) AS email_pending
FROM public."Users" u
LEFT JOIN public."Profiles" p ON u."ProfileId" = p."Id"
WHERE u."IsActive" = true
GROUP BY p."Name"
ORDER BY user_count DESC;

-- 2. Recent registrations (last 30 days)
SELECT
    u."Email",
    u."FirstName",
    u."LastName",
    p."Name" AS profile_type,
    u."IsEmailConfirmed",
    u."CreatedOn"
FROM public."Users" u
LEFT JOIN public."Profiles" p ON u."ProfileId" = p."Id"
WHERE u."CreatedOn" >= CURRENT_DATE - INTERVAL '30 days'
  AND u."IsActive" = true
ORDER BY u."CreatedOn" DESC;
