-- ============================================================
-- Script: 25_org_roles_and_permissions.sql
-- Purpose: Show all roles available in an organization (system-wide
--          and org-custom) with their assigned permissions and priority.
-- Tables:  organization_roles, organization_role_permissions, permissions
-- Usage:   Replace :org_id with the target organization UUID.
--          In psql: \set org_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
-- Notes:   System roles have organization_id IS NULL and apply to all
--          orgs. Custom roles have organization_id = :org_id.
--          priority: lower value = stronger role (0 = org_owner).
--          If a role has no permissions assigned, permissions_list
--          will be NULL.
-- ============================================================

SELECT
    r.id,
    r.name,
    r.display_name,
    r.priority,
    CASE WHEN r.organization_id IS NULL THEN 'system' ELSE 'custom' END AS role_type,
    r.is_archived,
    STRING_AGG(p.name, ', ' ORDER BY p.name) AS permissions_list
FROM organization_roles r
LEFT JOIN organization_role_permissions orp ON orp.org_role_id  = r.id
LEFT JOIN permissions                   p   ON p.id             = orp.permission_id
WHERE (r.organization_id = :'org_id' OR r.organization_id IS NULL)
  AND r.is_archived = FALSE
GROUP BY r.id, r.name, r.display_name, r.priority, r.organization_id, r.is_archived
ORDER BY r.priority ASC NULLS LAST, r.name;
