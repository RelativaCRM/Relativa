-- ============================================================
-- Script: 33_workspace_members_with_roles.sql
-- Purpose: List all active members of a workspace with their
--          assigned role and join date.
-- Tables:  user_role_workspace, users, workspace_roles
-- Usage:   Replace :workspace_id with the target workspace UUID.
--          In psql: \set workspace_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
-- Notes:   System workspace role names: ws_admin, ws_manager,
--          ws_analyst, ws_member. Lower priority = stronger role.
--          Remove the urw.is_archived filter to include archived
--          memberships.
-- ============================================================

SELECT
    u.id            AS user_id,
    u.first_name,
    u.last_name,
    u.email,
    r.name          AS role_name,
    r.display_name  AS role_display_name,
    r.priority      AS role_priority,
    urw.joined_at
FROM user_role_workspace urw
JOIN users           u ON u.id = urw.user_id
JOIN workspace_roles r ON r.id = urw.ws_role_id
WHERE urw.workspace_id = :'workspace_id'
  AND urw.is_archived  = FALSE
ORDER BY r.priority ASC, u.last_name, u.first_name;
