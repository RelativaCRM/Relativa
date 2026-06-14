-- ============================================================
-- Script: 12_user_by_id.sql
-- Purpose: Fetch a single user by UUID including their settings.
-- Tables:  users, user_settings
-- Usage:   Replace :user_id with the target user's UUID.
--          In psql: \set user_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
-- Notes:   Sensitive columns (password, tokens) are excluded.
--          Locale comes from user_settings (default: 'en').
-- ============================================================

SELECT
    u.id,
    u.first_name,
    u.last_name,
    u.email,
    u.phone,
    u.date_of_birth,
    u.created_at,
    u.email_verified,
    u.two_factor_enabled,
    u.is_archived,
    us.locale
FROM users u
LEFT JOIN user_settings us ON us.user_id = u.id
WHERE u.id = :'user_id';
