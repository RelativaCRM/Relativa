-- ============================================================
-- Script: 03_foreign_keys_map.sql
-- Purpose: List every foreign key constraint in the schema — which
--          column in which table references which other table/column,
--          and the ON DELETE / ON UPDATE behavior.
-- Tables:  information_schema.table_constraints,
--          information_schema.key_column_usage,
--          information_schema.constraint_column_usage,
--          information_schema.referential_constraints
-- Usage:   Run as-is. No parameters required.
--          To filter to a specific table add:
--            AND kcu.table_name = 'your_table'
-- Notes:   Useful for understanding CASCADE vs RESTRICT behaviors
--          before running deletes or archiving records.
-- ============================================================

SELECT
    kcu.table_name                AS source_table,
    kcu.column_name               AS source_column,
    ccu.table_name                AS target_table,
    ccu.column_name               AS target_column,
    rc.delete_rule                AS on_delete,
    rc.update_rule                AS on_update,
    tc.constraint_name
FROM information_schema.table_constraints     AS tc
JOIN information_schema.key_column_usage      AS kcu
    ON tc.constraint_name = kcu.constraint_name
    AND tc.table_schema   = kcu.table_schema
JOIN information_schema.constraint_column_usage AS ccu
    ON ccu.constraint_name = tc.constraint_name
    AND ccu.table_schema   = tc.table_schema
JOIN information_schema.referential_constraints AS rc
    ON rc.constraint_name = tc.constraint_name
WHERE tc.constraint_type = 'FOREIGN KEY'
  AND tc.table_schema    = 'public'
ORDER BY source_table, source_column;
