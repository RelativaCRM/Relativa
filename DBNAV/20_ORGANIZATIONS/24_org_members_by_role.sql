-- ============================================================
-- Script: 24_org_members_by_role.sql
-- Purpose: List organization members filtered to a specific role name.
-- Tables:  user_role_organization, users, organization_roles
-- Usage:   Replace :org_id and :role_name before running.
--          In psql:
--            \set org_id   'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
--            \set role_name 'org_owner'
--          System role names: org_owner, org_admin, org_member
--          Custom roles: use the exact name value from organization_roles.
-- Notes:   Role name matching is case-sensitive. Use script
--          25_org_roles_and_permissions.sql to see available role names.
-- ============================================================

SELECT
    u.id           AS user_id,
    u.first_name,
    u.last_name,
    u.email,
    u.email_verified,
    r.name         AS role_name,
    r.display_name AS role_display_name,
    uro.joined_at
FROM user_role_organization uro
JOIN users              u ON u.id = uro.user_id
JOIN organization_roles r ON r.id = uro.org_role_id
WHERE uro.organization_id = :'org_id'
  AND r.name              = :'role_name'
  AND uro.is_archived     = FALSE
ORDER BY u.last_name, u.first_name;
