-- ============================================================
-- Script: 01_all_tables_row_counts.sql
-- Purpose: List every table in the public schema with its estimated
--          and exact row counts.
-- Tables:  pg_stat_user_tables, information_schema.tables
-- Usage:   Run as-is. No parameters required.
-- Notes:   The "estimated" count uses PostgreSQL statistics (fast but
--          may be stale after bulk operations). The "exact" count uses
--          COUNT(*) and may be slow on large tables. The exact-count
--          block is commented out by default — uncomment if needed.
-- ============================================================

-- Fast estimate using pg_stat_user_tables (updated by ANALYZE)
SELECT
    schemaname                        AS schema_name,
    relname                           AS table_name,
    n_live_tup                        AS estimated_live_rows,
    n_dead_tup                        AS estimated_dead_rows,
    last_analyze                      AS last_analyzed,
    last_autovacuum                   AS last_autovacuumed
FROM pg_stat_user_tables
WHERE schemaname = 'public'
ORDER BY n_live_tup DESC;


-- ============================================================
-- ALTERNATIVE: Exact row counts (slow on large tables — use with care)
-- Uncomment the block below to get precise numbers.
-- ============================================================
/*
SELECT
    table_name,
    (xpath('/row/cnt/text()',
           query_to_xml(
               format('SELECT COUNT(*) AS cnt FROM public.%I', table_name),
               false, true, ''
           )
    ))[1]::text::bigint AS exact_row_count
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_type = 'BASE TABLE'
ORDER BY exact_row_count DESC;
*/
