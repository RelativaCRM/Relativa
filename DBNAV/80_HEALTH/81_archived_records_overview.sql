-- ============================================================
-- Script: 81_archived_records_overview.sql
-- Purpose: Count of archived vs live records across all tables
--          that use the is_archived soft-delete pattern. Useful
--          for spotting unusually high or unexpected archiving.
-- Tables:  users, organizations, workspaces, organization_roles,
--          workspace_roles, entity, user_role_organization,
--          user_role_workspace
-- Usage:   Run as-is. No parameters required.
-- Notes:   All Relativa user-facing tables use is_archived=TRUE
--          for soft deletes instead of physical row deletion.
-- ============================================================

SELECT table_name, live_count, archived_count
FROM (
    SELECT 'users'                AS table_name,
           COUNT(*) FILTER (WHERE NOT is_archived) AS live_count,
           COUNT(*) FILTER (WHERE is_archived)     AS archived_count
    FROM users

    UNION ALL

    SELECT 'organizations',
           COUNT(*) FILTER (WHERE NOT is_archived),
           COUNT(*) FILTER (WHERE is_archived)
    FROM organizations

    UNION ALL

    SELECT 'workspaces',
           COUNT(*) FILTER (WHERE NOT is_archived),
           COUNT(*) FILTER (WHERE is_archived)
    FROM workspaces

    UNION ALL

    SELECT 'organization_roles',
           COUNT(*) FILTER (WHERE NOT is_archived),
           COUNT(*) FILTER (WHERE is_archived)
    FROM organization_roles

    UNION ALL

    SELECT 'workspace_roles',
           COUNT(*) FILTER (WHERE NOT is_archived),
           COUNT(*) FILTER (WHERE is_archived)
    FROM workspace_roles

    UNION ALL

    SELECT 'entity',
           COUNT(*) FILTER (WHERE NOT is_archived),
           COUNT(*) FILTER (WHERE is_archived)
    FROM entity

    UNION ALL

    SELECT 'user_role_organization',
           COUNT(*) FILTER (WHERE NOT is_archived),
           COUNT(*) FILTER (WHERE is_archived)
    FROM user_role_organization

    UNION ALL

    SELECT 'user_role_workspace',
           COUNT(*) FILTER (WHERE NOT is_archived),
           COUNT(*) FILTER (WHERE is_archived)
    FROM user_role_workspace
) t
ORDER BY table_name;
