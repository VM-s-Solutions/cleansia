-- Database Health Check
-- Read-only diagnostic queries

-- 1. Table row counts
SELECT
    schemaname AS schema,
    relname AS table_name,
    n_live_tup AS row_count
FROM pg_stat_user_tables
ORDER BY n_live_tup DESC;

-- 2. Database size
SELECT pg_size_pretty(pg_database_size(current_database())) AS database_size;

-- 3. Largest tables by size
SELECT
    relname AS table_name,
    pg_size_pretty(pg_total_relation_size(relid)) AS total_size,
    pg_size_pretty(pg_relation_size(relid)) AS table_size,
    pg_size_pretty(pg_total_relation_size(relid) - pg_relation_size(relid)) AS index_size
FROM pg_catalog.pg_statio_user_tables
ORDER BY pg_total_relation_size(relid) DESC
LIMIT 20;

-- 4. Active connections
SELECT
    state,
    COUNT(*) AS count
FROM pg_stat_activity
WHERE datname = current_database()
GROUP BY state;

-- 5. Long-running queries (> 30 seconds)
SELECT
    pid,
    now() - pg_stat_activity.query_start AS duration,
    query,
    state
FROM pg_stat_activity
WHERE (now() - pg_stat_activity.query_start) > interval '30 seconds'
  AND datname = current_database()
  AND state != 'idle'
ORDER BY duration DESC;
