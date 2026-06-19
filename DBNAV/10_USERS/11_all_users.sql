-- ============================================================
-- Script: 11_all_users.sql
-- Purpose: List all non-archived users with basic identity and
--          security-status info.
-- Tables:  users
-- Usage:   Run as-is to see all active users.
--          To include archived users, remove or comment out the
--          WHERE clause below.
-- Notes:   Passwords and tokens are excluded intentionally.
--          email_verified=false may indicate a user who never
--          completed registration.
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
-- WHERE is_archived = FALSE   -- uncomment to exclude archived users
ORDER BY created_at DESC;
