-- ============================================================
-- Script: 64_search_user_by_name_or_email.sql
-- Purpose: Search the users table by partial first name, last name,
--          or email address (case-insensitive).
-- Tables:  users
-- Usage:   Replace :search_term with the string to search for.
--          In psql: \set search_term 'smith'
-- Notes:   Searches all three fields with OR, so a hit on any one
--          field returns the row. Includes archived users — filter
--          by is_archived if needed.
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
WHERE first_name ILIKE '%' || :'search_term' || '%'
   OR last_name  ILIKE '%' || :'search_term' || '%'
   OR email      ILIKE '%' || :'search_term' || '%'
ORDER BY is_archived ASC, last_name, first_name;
