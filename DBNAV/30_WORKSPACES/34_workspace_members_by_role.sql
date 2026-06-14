-- ============================================================
-- Script: 34_workspace_members_by_role.sql
-- Purpose: List workspace members filtered to a specific role name.
-- Tables:  user_role_workspace, users, workspace_roles
-- Usage:   Replace :workspace_id and :role_name before running.
--          In psql:
--            \set workspace_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
--            \set role_name    'ws_admin'
--          System role names: ws_admin, ws_manager, ws_analyst, ws_member
--          Custom roles: use the exact name from workspace_roles table.
-- Notes:   Role name is case-sensitive. Use script
--          35_workspace_roles_and_permissions.sql to list available names.
-- ============================================================

SELECT
    u.id           AS user_id,
    u.first_name,
    u.last_name,
    u.email,
    r.name         AS role_name,
    r.display_name AS role_display_name,
    urw.joined_at
FROM user_role_workspace urw
JOIN users           u ON u.id = urw.user_id
JOIN workspace_roles r ON r.id = urw.ws_role_id
WHERE urw.workspace_id = :'workspace_id'
  AND r.name           = :'role_name'
  AND urw.is_archived  = FALSE
ORDER BY u.last_name, u.first_name;
