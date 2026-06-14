-- ============================================================
-- Script: 13_user_by_email.sql
-- Purpose: Find a user by email address (case-insensitive match).
-- Tables:  users
-- Usage:   Replace :email with the address to look up.
--          In psql: \set email 'someone@example.com'
-- Notes:   The active-user unique index is a partial index on
--          LOWER(email) WHERE is_archived = FALSE, so duplicate
--          emails may exist across archived and active records.
--          This query searches all rows; filter is_archived if needed.
-- ============================================================

SELECT
    id,
    first_name,
    last_name,
    email,
    created_at,
    email_verified,
    two_factor_enabled,
    is_archived
FROM users
WHERE LOWER(email) = LOWER(:'email')
ORDER BY is_archived ASC, created_at DESC;
