-- ============================================================
-- Script: 43_role_permissions_workspace.sql
-- Purpose: For every workspace role, show which permissions it holds.
--          Covers both system roles and any custom roles for a given
--          workspace.
-- Tables:  workspace_roles, workspace_role_permissions, permissions
-- Usage:   Replace :workspace_id with a specific workspace UUID to
--          include custom roles for that workspace.
--          In psql: \set workspace_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
-- Notes:   System workspace role names: ws_admin (priority 0),
--          ws_manager (priority 2), ws_analyst (priority 4),
--          ws_member (priority 6).
-- ============================================================

SELECT
    r.id                AS role_id,
    r.name              AS role_name,
    r.display_name,
    r.priority,
    CASE WHEN r.workspace_id IS NULL THEN 'system' ELSE 'custom' END AS role_type,
    p.name              AS permission_name,
    p.display_name      AS permission_display_name
FROM workspace_roles r
LEFT JOIN workspace_role_permissions wrp ON wrp.ws_role_id  = r.id
LEFT JOIN permissions                p   ON p.id            = wrp.permission_id
WHERE (r.workspace_id = :'workspace_id' OR r.workspace_id IS NULL)
  AND r.is_archived = FALSE
ORDER BY r.priority ASC NULLS LAST, r.name, p.name;
