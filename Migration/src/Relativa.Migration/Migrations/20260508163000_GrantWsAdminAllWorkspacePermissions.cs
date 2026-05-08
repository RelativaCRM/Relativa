using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

/// <summary>
/// Ensures every workspace-scoped permission used by the app exists (by name) and grants
/// the system <c>ws_admin</c> role the full set, including entity CRUD and <c>delete_workspace</c>.
/// Uses names (not fixed ids) so this stays correct across environments.
/// </summary>
[DbContext(typeof(MigrationDbContext))]
[Migration("20260508163000_GrantWsAdminAllWorkspacePermissions")]
public partial class GrantWsAdminAllWorkspacePermissions : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            -- Canonical workspace permission names (see WorkspacePermissions.FullWorkspaceAuthority).
            INSERT INTO permissions (name, is_archived)
            SELECT v.name, FALSE
            FROM (VALUES
                ('manage_ws_settings'),
                ('add_ws_members'),
                ('remove_ws_members'),
                ('assign_ws_roles'),
                ('manage_ws_roles'),
                ('create_entities'),
                ('edit_entities'),
                ('delete_entities'),
                ('view_entities'),
                ('view_analytics'),
                ('delete_workspace')
            ) AS v(name)
            WHERE NOT EXISTS (SELECT 1 FROM permissions p WHERE p.name = v.name);

            SELECT setval(pg_get_serial_sequence('permissions', 'id'),
                          (SELECT COALESCE(MAX(id), 1) FROM permissions));

            INSERT INTO workspace_role_permissions (ws_role_id, permission_id)
            SELECT wr.id, p.id
            FROM workspace_roles wr
            CROSS JOIN permissions p
            WHERE wr.workspace_id IS NULL
              AND wr.name = 'ws_admin'
              AND p.name IN (
                  'manage_ws_settings',
                  'add_ws_members',
                  'remove_ws_members',
                  'assign_ws_roles',
                  'manage_ws_roles',
                  'create_entities',
                  'edit_entities',
                  'delete_entities',
                  'view_entities',
                  'view_analytics',
                  'delete_workspace'
              )
              AND NOT EXISTS (
                  SELECT 1
                  FROM workspace_role_permissions x
                  WHERE x.ws_role_id = wr.id
                    AND x.permission_id = p.id
              );

            SELECT setval(pg_get_serial_sequence('workspace_role_permissions', 'id'),
                          (SELECT COALESCE(MAX(id), 1) FROM workspace_role_permissions));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM workspace_role_permissions wrp
            USING workspace_roles wr, permissions p
            WHERE wrp.ws_role_id = wr.id
              AND wrp.permission_id = p.id
              AND wr.workspace_id IS NULL
              AND wr.name = 'ws_admin'
              AND p.name IN (
                  'manage_ws_settings',
                  'add_ws_members',
                  'remove_ws_members',
                  'assign_ws_roles',
                  'manage_ws_roles',
                  'create_entities',
                  'edit_entities',
                  'delete_entities',
                  'view_entities',
                  'view_analytics',
                  'delete_workspace'
              );

            SELECT setval(pg_get_serial_sequence('workspace_role_permissions', 'id'),
                          (SELECT COALESCE(MAX(id), 1) FROM workspace_role_permissions));
            """);
    }
}
