-- ============================================================
-- Script: 31_all_workspaces.sql
-- Purpose: List all workspaces across all organizations with org
--          name and active member count. Useful for a global
--          view of workspace sprawl.
-- Tables:  workspaces, organizations, user_role_workspace
-- Usage:   Run as-is. Add AND w.is_archived = FALSE to hide
--          archived workspaces (currently uncommented below).
-- Notes:   Archived workspaces are hidden by default. To include
--          them remove the filter on w.is_archived.
-- ============================================================

SELECT
    w.id             AS workspace_id,
    w.name           AS workspace_name,
    o.id             AS org_id,
    o.name           AS org_name,
    w.is_archived    AS workspace_archived,
    COUNT(urw.id)
        FILTER (WHERE urw.is_archived = FALSE) AS active_member_count
FROM workspaces w
JOIN organizations    o   ON o.id = w.organization_id
LEFT JOIN user_role_workspace urw ON urw.workspace_id = w.id
WHERE w.is_archived = FALSE
GROUP BY w.id, w.name, o.id, o.name, w.is_archived
ORDER BY o.name, w.name;
