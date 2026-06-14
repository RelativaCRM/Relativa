-- ============================================================
-- Script: 42_role_permissions_org.sql
-- Purpose: For every organization role, show which permissions it
--          holds. Covers both system roles and any custom roles for
--          a given organization.
-- Tables:  organization_roles, organization_role_permissions, permissions
-- Usage:   Replace :org_id with a specific organization UUID to include
--          custom roles for that org, OR set it to NULL to show only
--          system roles.
--          In psql: \set org_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
--          To show ONLY system roles: remove the OR clause and keep
--          WHERE r.organization_id IS NULL.
-- Notes:   Rows without permission entries mean the role has no
--          permissions assigned (NULL in permission columns).
-- ============================================================

SELECT
    r.id                AS role_id,
    r.name              AS role_name,
    r.display_name,
    r.priority,
    CASE WHEN r.organization_id IS NULL THEN 'system' ELSE 'custom' END AS role_type,
    p.name              AS permission_name,
    p.display_name      AS permission_display_name
FROM organization_roles r
LEFT JOIN organization_role_permissions orp ON orp.org_role_id  = r.id
LEFT JOIN permissions                   p   ON p.id             = orp.permission_id
WHERE (r.organization_id = :'org_id' OR r.organization_id IS NULL)
  AND r.is_archived = FALSE
ORDER BY r.priority ASC NULLS LAST, r.name, p.name;
