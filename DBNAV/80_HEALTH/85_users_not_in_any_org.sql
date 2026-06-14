-- ============================================================
-- Script: 85_users_not_in_any_org.sql
-- Purpose: Find active users who have no active organization
--          membership. These users can authenticate but will see
--          no content in the application.
-- Tables:  users, user_role_organization
-- Usage:   Run as-is. No parameters required.
-- Notes:   This is expected for brand-new users who have not yet
--          joined or been invited to an organization. A large number
--          of such users may indicate an onboarding funnel issue
--          or orphaned accounts from failed invite acceptance.
-- ============================================================

SELECT
    u.id,
    u.first_name,
    u.last_name,
    u.email,
    u.created_at,
    u.email_verified
FROM users u
WHERE u.is_archived = FALSE
  AND NOT EXISTS (
      SELECT 1
      FROM user_role_organization uro
      WHERE uro.user_id    = u.id
        AND uro.is_archived = FALSE
  )
ORDER BY u.created_at DESC;
