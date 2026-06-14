-- ============================================================
-- Script: 21_all_organizations.sql
-- Purpose: List all organizations with their settings, active member
--          count, and workspace count.
-- Tables:  organizations, organization_settings, user_role_organization,
--          workspaces
-- Usage:   Run as-is. Remove the WHERE clause to include archived orgs.
-- Notes:   member_count reflects active (non-archived) memberships.
--          workspace_count includes both active and archived workspaces
--          — adjust the subquery filter if needed.
-- ============================================================

SELECT
    o.id,
    o.name,
    os.description,
    os.join_policy,
    o.is_archived,
    COUNT(DISTINCT uro.id)
        FILTER (WHERE uro.is_archived = FALSE) AS active_member_count,
    COUNT(DISTINCT w.id)                        AS workspace_count
FROM organizations o
LEFT JOIN organization_settings  os  ON os.organization_id = o.id
LEFT JOIN user_role_organization uro ON uro.organization_id = o.id
LEFT JOIN workspaces             w   ON w.organization_id   = o.id
WHERE o.is_archived = FALSE
GROUP BY o.id, o.name, os.description, os.join_policy, o.is_archived
ORDER BY o.name;
