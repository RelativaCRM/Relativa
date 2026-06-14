-- ============================================================
-- Script: 84_roles_without_permissions.sql
-- Purpose: Find organization and workspace roles that have no
--          permissions assigned. Highlights custom roles that may
--          have been created but not fully configured.
-- Tables:  organization_roles, organization_role_permissions,
--          workspace_roles, workspace_role_permissions
-- Usage:   Run as-is. No parameters required.
-- Notes:   The system role 'org_member' intentionally has no
--          permissions by default — this is expected. Flag any
--          *custom* roles (role_type = 'custom') with no permissions
--          as they are likely misconfigured.
-- ============================================================

SELECT
    'organization' AS role_domain,
    r.id           AS role_id,
    r.name         AS role_name,
    r.display_name,
    r.priority,
    CASE WHEN r.organization_id IS NULL THEN 'system' ELSE 'custom' END AS role_type,
    r.organization_id AS scoped_to
FROM organization_roles r
WHERE r.is_archived = FALSE
  AND NOT EXISTS (
      SELECT 1 FROM organization_role_permissions orp
      WHERE orp.org_role_id = r.id
  )

UNION ALL

SELECT
    'workspace'    AS role_domain,
    r.id           AS role_id,
    r.name         AS role_name,
    r.display_name,
    r.priority,
    CASE WHEN r.workspace_id IS NULL THEN 'system' ELSE 'custom' END AS role_type,
    r.workspace_id AS scoped_to
FROM workspace_roles r
WHERE r.is_archived = FALSE
  AND NOT EXISTS (
      SELECT 1 FROM workspace_role_permissions wrp
      WHERE wrp.ws_role_id = r.id
  )

ORDER BY role_type DESC, role_domain, role_name;
