-- ============================================================
-- Script: 15_user_2fa_status.sql
-- Purpose: Overview of all active users showing whether 2FA is
--          enabled and how many unused backup codes they have left.
-- Tables:  users, user_backup_code
-- Usage:   Run as-is. Filter by two_factor_enabled if needed.
-- Notes:   Users with two_factor_enabled=TRUE and 0 unused backup
--          codes are locked out if they lose their authenticator app.
--          This script helps identify such risky configurations.
-- ============================================================

SELECT
    u.id,
    u.first_name,
    u.last_name,
    u.email,
    u.two_factor_enabled,
    COUNT(bc.id) FILTER (WHERE bc.used_at IS NULL)     AS unused_backup_codes,
    COUNT(bc.id) FILTER (WHERE bc.used_at IS NOT NULL) AS used_backup_codes
FROM users u
LEFT JOIN user_backup_code bc ON bc.user_id = u.id
WHERE u.is_archived = FALSE
GROUP BY u.id, u.first_name, u.last_name, u.email, u.two_factor_enabled
ORDER BY u.two_factor_enabled DESC, u.last_name, u.first_name;
