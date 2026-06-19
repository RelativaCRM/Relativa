-- ============================================================
-- Script: 45_user_effective_workspace_permissions.sql
-- Purpose: Show the exact workspace permissions a specific user has
--          in a specific workspace, resolved through their role.
-- Tables:  user_role_workspace, workspace_roles,
--          workspace_role_permissions, permissions
-- Usage:   Replace :user_id and :workspace_id before running.
--          In psql:
--            \set user_id      'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
--            \set workspace_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
-- Notes:   0 rows means the user has no active membership in that
--          workspace. Check user_role_workspace directly if unexpected.
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
FROM user_role_workspace urw
JOIN users           u ON u.id  = urw.user_id
JOIN workspace_roles r ON r.id  = urw.ws_role_id
LEFT JOIN workspace_role_permissions wrp ON wrp.ws_role_id = r.id
LEFT JOIN permissions                p   ON p.id           = wrp.permission_id
WHERE urw.user_id      = :'user_id'
  AND urw.workspace_id = :'workspace_id'
  AND urw.is_archived  = FALSE
ORDER BY p.name;
