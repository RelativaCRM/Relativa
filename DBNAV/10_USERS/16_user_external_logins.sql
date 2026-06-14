-- ============================================================
-- Script: 16_user_external_logins.sql
-- Purpose: List all users who have linked external OAuth/SAML
--          providers, with the provider name and linked subject.
-- Tables:  users, user_external_login
-- Usage:   Run as-is to see all external logins.
--          To filter by provider, add: AND uel.provider = 'google'
-- Notes:   Users without a password (u.password IS NULL) rely
--          exclusively on external logins to authenticate.
--          The subject is the external provider's user identifier
--          (e.g. Google sub, SAML NameID).
-- ============================================================

SELECT
    u.id           AS user_id,
    u.email,
    u.first_name,
    u.last_name,
    uel.provider,
    uel.subject,
    uel.created_at AS linked_at,
    u.password IS NULL AS password_login_disabled
FROM user_external_login uel
JOIN users u ON u.id = uel.user_id
WHERE u.is_archived = FALSE
ORDER BY uel.provider, u.email;
