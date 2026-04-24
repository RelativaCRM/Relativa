using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <summary>
    /// Replaces deal-specific workspace permissions (edit_deals / view_deals) with
    /// entity-scoped permissions (manage_entities / view_entities).  Deals are now
    /// just another entity_type, so a separate permission surface is redundant.
    ///
    /// Performs a FK-safe full reseed: clears join tables before permissions, then
    /// re-inserts the canonical 16-permission set and all role-permission grants.
    /// </summary>
    public partial class ReseedPermissions : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- ----------------------------------------------------------------
-- 1. Clear join tables first (FK order matters)
-- ----------------------------------------------------------------
DELETE FROM workspace_role_permissions;
DELETE FROM organization_role_permissions;
DELETE FROM permissions;

-- ----------------------------------------------------------------
-- 2. Re-insert canonical permission set (16 rows, same ids)
--    ids 1-7  : organization-scoped
--    ids 8-16 : workspace-scoped
--    id 14 was edit_deals  → manage_entities
--    id 15 was view_deals  → view_entities
-- ----------------------------------------------------------------
INSERT INTO permissions (id, name, is_archived) VALUES
-- Organization-scoped
(1,  'manage_org_settings',  FALSE),
(2,  'invite_to_org',        FALSE),
(3,  'manage_join_requests', FALSE),
(4,  'remove_org_members',   FALSE),
(5,  'assign_org_roles',     FALSE),
(6,  'manage_org_roles',     FALSE),
(7,  'create_workspaces',    FALSE),
-- Workspace-scoped
(8,  'manage_ws_settings',   FALSE),
(9,  'invite_to_workspace',  FALSE),
(10, 'add_ws_members',       FALSE),
(11, 'remove_ws_members',    FALSE),
(12, 'assign_ws_roles',      FALSE),
(13, 'manage_ws_roles',      FALSE),
(14, 'manage_entities',      FALSE),
(15, 'view_entities',        FALSE),
(16, 'view_analytics',       FALSE);

-- ----------------------------------------------------------------
-- 3. Organization role-permission grants
-- ----------------------------------------------------------------
-- org_owner: all 7 org permissions (1-7)
INSERT INTO organization_role_permissions (id, org_role_id, permission_id) VALUES
(1, 1, 1), (2, 1, 2), (3, 1, 3), (4, 1, 4), (5, 1, 5), (6, 1, 6), (7, 1, 7);

-- org_admin: all org permissions except manage_org_roles (6)
INSERT INTO organization_role_permissions (id, org_role_id, permission_id) VALUES
(8, 2, 1), (9, 2, 2), (10, 2, 3), (11, 2, 4), (12, 2, 5), (13, 2, 7);

-- ----------------------------------------------------------------
-- 4. Workspace role-permission grants
-- ----------------------------------------------------------------
-- ws_admin: all 9 workspace permissions (8-16)
INSERT INTO workspace_role_permissions (id, ws_role_id, permission_id) VALUES
(1, 1, 8), (2, 1, 9), (3, 1, 10), (4, 1, 11), (5, 1, 12), (6, 1, 13), (7, 1, 14), (8, 1, 15), (9, 1, 16);

-- ws_manager: invite_to_workspace, add_ws_members, manage_entities, view_entities, view_analytics
INSERT INTO workspace_role_permissions (id, ws_role_id, permission_id) VALUES
(10, 2, 9), (11, 2, 10), (12, 2, 14), (13, 2, 15), (14, 2, 16);

-- ws_analyst: view_analytics, view_entities
INSERT INTO workspace_role_permissions (id, ws_role_id, permission_id) VALUES
(15, 3, 15), (16, 3, 16);

-- ws_member: view_entities
INSERT INTO workspace_role_permissions (id, ws_role_id, permission_id) VALUES
(17, 4, 15);

-- ----------------------------------------------------------------
-- 5. Reset sequences
-- ----------------------------------------------------------------
SELECT setval(pg_get_serial_sequence('permissions', 'id'), (SELECT COALESCE(MAX(id),1) FROM permissions));
SELECT setval(pg_get_serial_sequence('organization_role_permissions', 'id'), (SELECT COALESCE(MAX(id),1) FROM organization_role_permissions));
SELECT setval(pg_get_serial_sequence('workspace_role_permissions', 'id'), (SELECT COALESCE(MAX(id),1) FROM workspace_role_permissions));
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- Restore deal-specific permissions (reverses the rename at ids 14 and 15)
DELETE FROM workspace_role_permissions;
DELETE FROM organization_role_permissions;
DELETE FROM permissions;

INSERT INTO permissions (id, name, is_archived) VALUES
(1,  'manage_org_settings',  FALSE),
(2,  'invite_to_org',        FALSE),
(3,  'manage_join_requests', FALSE),
(4,  'remove_org_members',   FALSE),
(5,  'assign_org_roles',     FALSE),
(6,  'manage_org_roles',     FALSE),
(7,  'create_workspaces',    FALSE),
(8,  'manage_ws_settings',   FALSE),
(9,  'invite_to_workspace',  FALSE),
(10, 'add_ws_members',       FALSE),
(11, 'remove_ws_members',    FALSE),
(12, 'assign_ws_roles',      FALSE),
(13, 'manage_ws_roles',      FALSE),
(14, 'edit_deals',           FALSE),
(15, 'view_deals',           FALSE),
(16, 'view_analytics',       FALSE);

INSERT INTO organization_role_permissions (id, org_role_id, permission_id) VALUES
(1, 1, 1), (2, 1, 2), (3, 1, 3), (4, 1, 4), (5, 1, 5), (6, 1, 6), (7, 1, 7);
INSERT INTO organization_role_permissions (id, org_role_id, permission_id) VALUES
(8, 2, 1), (9, 2, 2), (10, 2, 3), (11, 2, 4), (12, 2, 5), (13, 2, 7);

INSERT INTO workspace_role_permissions (id, ws_role_id, permission_id) VALUES
(1, 1, 8), (2, 1, 9), (3, 1, 10), (4, 1, 11), (5, 1, 12), (6, 1, 13), (7, 1, 14), (8, 1, 15), (9, 1, 16);
INSERT INTO workspace_role_permissions (id, ws_role_id, permission_id) VALUES
(10, 2, 9), (11, 2, 10), (12, 2, 14), (13, 2, 15), (14, 2, 16);
INSERT INTO workspace_role_permissions (id, ws_role_id, permission_id) VALUES
(15, 3, 15), (16, 3, 16);
INSERT INTO workspace_role_permissions (id, ws_role_id, permission_id) VALUES
(17, 4, 15);

SELECT setval(pg_get_serial_sequence('permissions', 'id'), (SELECT COALESCE(MAX(id),1) FROM permissions));
SELECT setval(pg_get_serial_sequence('organization_role_permissions', 'id'), (SELECT COALESCE(MAX(id),1) FROM organization_role_permissions));
SELECT setval(pg_get_serial_sequence('workspace_role_permissions', 'id'), (SELECT COALESCE(MAX(id),1) FROM workspace_role_permissions));
");
        }
    }
}
