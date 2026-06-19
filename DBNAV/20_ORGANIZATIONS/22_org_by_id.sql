-- ============================================================
-- Script: 22_org_by_id.sql
-- Purpose: Full detail for a single organization including settings,
--          member count, and workspace count.
-- Tables:  organizations, organization_settings, user_role_organization,
--          workspaces, organization_roles
-- Usage:   Replace :org_id with the target organization UUID.
--          In psql: \set org_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
-- Notes:   The default_role subquery resolves the role name from the
--          default_org_role_id FK stored in organization_settings.
-- ============================================================

SELECT
    o.id,
    o.name,
    o.is_archived,
    os.description,
    os.join_policy,
    defr.name        AS default_member_role,
    COUNT(DISTINCT uro.id)
        FILTER (WHERE uro.is_archived = FALSE) AS active_member_count,
    COUNT(DISTINCT w.id)
        FILTER (WHERE w.is_archived = FALSE)   AS active_workspace_count
FROM organizations o
LEFT JOIN organization_settings  os   ON os.organization_id = o.id
LEFT JOIN organization_roles     defr ON defr.id = os.default_org_role_id
LEFT JOIN user_role_organization uro  ON uro.organization_id = o.id
LEFT JOIN workspaces             w    ON w.organization_id   = o.id
WHERE o.id = :'org_id'
GROUP BY o.id, o.name, o.is_archived, os.description, os.join_policy,
         defr.name;
