-- ============================================================
-- Script: 35_workspace_roles_and_permissions.sql
-- Purpose: Show all roles available in a workspace (system-wide
--          and workspace-custom) with their assigned permissions.
-- Tables:  workspace_roles, workspace_role_permissions, permissions
-- Usage:   Replace :workspace_id with the target workspace UUID.
--          In psql: \set workspace_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
-- Notes:   System roles have workspace_id IS NULL (apply to all
--          workspaces). Custom roles have workspace_id = :workspace_id.
--          priority: lower value = stronger role (0 = ws_admin).
-- ============================================================

SELECT
    r.id,
    r.name,
    r.display_name,
    r.priority,
    CASE WHEN r.workspace_id IS NULL THEN 'system' ELSE 'custom' END AS role_type,
    r.is_archived,
    STRING_AGG(p.name, ', ' ORDER BY p.name) AS permissions_list
FROM workspace_roles r
LEFT JOIN workspace_role_permissions wrp ON wrp.ws_role_id   = r.id
LEFT JOIN permissions                p   ON p.id             = wrp.permission_id
WHERE (r.workspace_id = :'workspace_id' OR r.workspace_id IS NULL)
  AND r.is_archived = FALSE
GROUP BY r.id, r.name, r.display_name, r.priority, r.workspace_id, r.is_archived
ORDER BY r.priority ASC NULLS LAST, r.name;
