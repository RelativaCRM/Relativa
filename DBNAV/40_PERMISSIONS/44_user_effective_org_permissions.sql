-- ============================================================
-- Script: 44_user_effective_org_permissions.sql
-- Purpose: Show the exact organization permissions a specific user
--          has in a specific organization, resolved through their
--          assigned role.
-- Tables:  user_role_organization, organization_roles,
--          organization_role_permissions, permissions
-- Usage:   Replace :user_id and :org_id before running.
--          In psql:
--            \set user_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
--            \set org_id  'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
-- Notes:   If the user has no active membership in the org, this
--          returns 0 rows. If their role has no permissions, the
--          membership row is returned but permission columns are NULL.
-- ============================================================

SELECT
    u.id              AS user_id,
    u.first_name,
    u.last_name,
    u.email,
    r.name            AS role_name,
    r.display_name    AS role_display_name,
    r.priority        AS role_priority,
    p.name            AS permission_name,
    p.display_name    AS permission_display_name
FROM user_role_organization uro
JOIN users              u ON u.id  = uro.user_id
JOIN organization_roles r ON r.id  = uro.org_role_id
LEFT JOIN organization_role_permissions orp ON orp.org_role_id = r.id
LEFT JOIN permissions                   p   ON p.id            = orp.permission_id
WHERE uro.user_id         = :'user_id'
  AND uro.organization_id = :'org_id'
  AND uro.is_archived     = FALSE
ORDER BY p.name;
