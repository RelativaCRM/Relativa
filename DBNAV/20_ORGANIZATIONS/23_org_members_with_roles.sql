-- ============================================================
-- Script: 23_org_members_with_roles.sql
-- Purpose: List all active members of an organization with their
--          assigned role and join date, sorted by role priority
--          (strongest role first) then alphabetically.
-- Tables:  user_role_organization, users, organization_roles
-- Usage:   Replace :org_id with the target organization UUID.
--          In psql: \set org_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
-- Notes:   Lower priority value = stronger role (owner=0, admin=1, etc.).
--          To include archived memberships remove the uro.is_archived
--          filter below.
-- ============================================================

SELECT
    u.id            AS user_id,
    u.first_name,
    u.last_name,
    u.email,
    r.name          AS role_name,
    r.display_name  AS role_display_name,
    r.priority      AS role_priority,
    uro.joined_at,
    uro.is_archived AS membership_archived
FROM user_role_organization uro
JOIN users              u ON u.id  = uro.user_id
JOIN organization_roles r ON r.id  = uro.org_role_id
WHERE uro.organization_id = :'org_id'
  AND uro.is_archived = FALSE
ORDER BY r.priority ASC, u.last_name, u.first_name;
