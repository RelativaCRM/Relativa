-- ============================================================
-- Script: 02_schema_columns_explorer.sql
-- Purpose: List all columns for a given table — name, ordinal position,
--          data type, nullable flag, and default value.
-- Tables:  information_schema.columns
-- Usage:   Replace :table_name with the target table name (e.g. 'users').
--          In psql: \set table_name 'users'  then run the query.
--          In other clients: replace :'table_name' with a quoted string.
-- Notes:   Table names in PostgreSQL are case-sensitive when quoted.
--          All Relativa tables use lowercase snake_case names.
-- ============================================================

SELECT
    ordinal_position                                   AS "#",
    column_name,
    data_type
        || CASE
               WHEN character_maximum_length IS NOT NULL
               THEN '(' || character_maximum_length || ')'
               WHEN numeric_precision IS NOT NULL AND numeric_scale IS NOT NULL
               THEN '(' || numeric_precision || ',' || numeric_scale || ')'
               ELSE ''
           END                                         AS full_type,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name   = :'table_name'
ORDER BY ordinal_position;
