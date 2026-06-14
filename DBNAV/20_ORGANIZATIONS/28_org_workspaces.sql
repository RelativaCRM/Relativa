-- ============================================================
-- Script: 28_org_workspaces.sql
-- Purpose: List all workspaces in an organization with creator info,
--          settings, and active member count.
-- Tables:  workspaces, workspace_settings, users, user_role_workspace
-- Usage:   Replace :org_id with the target organization UUID.
--          In psql: \set org_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
-- Notes:   Remove the w.is_archived = FALSE filter to also see
--          archived workspaces. active_member_count excludes archived
--          workspace memberships.
-- ============================================================

SELECT
    w.id,
    w.name,
    w.is_archived,
    ws.description,
    ws.risk_scoring_enabled,
    ws.high_risk_threshold,
    ws.medium_risk_threshold,
    u.first_name || ' ' || u.last_name AS created_by,
    COUNT(urw.id)
        FILTER (WHERE urw.is_archived = FALSE) AS active_member_count
FROM workspaces w
LEFT JOIN workspace_settings  ws  ON ws.workspace_id    = w.id
LEFT JOIN users               u   ON u.id               = w.created_by_user_id
LEFT JOIN user_role_workspace urw ON urw.workspace_id   = w.id
WHERE w.organization_id = :'org_id'
  AND w.is_archived = FALSE
GROUP BY w.id, w.name, w.is_archived, ws.description,
         ws.risk_scoring_enabled, ws.high_risk_threshold,
         ws.medium_risk_threshold, u.first_name, u.last_name
ORDER BY w.name;
