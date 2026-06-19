using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

/// <summary>
/// Adds two new workspace permissions:
///   - view_basic_stats   → granted to ws_member, ws_analyst, ws_manager, ws_admin
///   - view_team_analytics → granted to ws_manager, ws_admin
/// These permissions drive permission-based dashboard content gating.
/// </summary>
[DbContext(typeof(MigrationDbContext))]
[Migration("20260521100000_AddDashboardPermissions")]
public partial class AddDashboardPermissions : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO permissions (name, is_archived)
            SELECT 'view_basic_stats', FALSE
            WHERE NOT EXISTS (SELECT 1 FROM permissions WHERE name = 'view_basic_stats');

            INSERT INTO permissions (name, is_archived)
            SELECT 'view_team_analytics', FALSE
            WHERE NOT EXISTS (SELECT 1 FROM permissions WHERE name = 'view_team_analytics');

            SELECT setval(pg_get_serial_sequence('permissions', 'id'),
                          (SELECT COALESCE(MAX(id), 1) FROM permissions));

            -- ws_member → view_basic_stats
            INSERT INTO workspace_role_permissions (ws_role_id, permission_id)
            SELECT wr.id, p.id
            FROM workspace_roles wr
            CROSS JOIN permissions p
            WHERE wr.workspace_id IS NULL AND wr.name = 'ws_member'
              AND p.name = 'view_basic_stats'
              AND NOT EXISTS (
                  SELECT 1 FROM workspace_role_permissions x
                  WHERE x.ws_role_id = wr.id AND x.permission_id = p.id
              );

            -- ws_analyst → view_basic_stats
            INSERT INTO workspace_role_permissions (ws_role_id, permission_id)
            SELECT wr.id, p.id
            FROM workspace_roles wr
            CROSS JOIN permissions p
            WHERE wr.workspace_id IS NULL AND wr.name = 'ws_analyst'
              AND p.name = 'view_basic_stats'
              AND NOT EXISTS (
                  SELECT 1 FROM workspace_role_permissions x
                  WHERE x.ws_role_id = wr.id AND x.permission_id = p.id
              );

            -- ws_manager → view_basic_stats
            INSERT INTO workspace_role_permissions (ws_role_id, permission_id)
            SELECT wr.id, p.id
            FROM workspace_roles wr
            CROSS JOIN permissions p
            WHERE wr.workspace_id IS NULL AND wr.name = 'ws_manager'
              AND p.name = 'view_basic_stats'
              AND NOT EXISTS (
                  SELECT 1 FROM workspace_role_permissions x
                  WHERE x.ws_role_id = wr.id AND x.permission_id = p.id
              );

            -- ws_manager → view_team_analytics
            INSERT INTO workspace_role_permissions (ws_role_id, permission_id)
            SELECT wr.id, p.id
            FROM workspace_roles wr
            CROSS JOIN permissions p
            WHERE wr.workspace_id IS NULL AND wr.name = 'ws_manager'
              AND p.name = 'view_team_analytics'
              AND NOT EXISTS (
                  SELECT 1 FROM workspace_role_permissions x
                  WHERE x.ws_role_id = wr.id AND x.permission_id = p.id
              );

            -- ws_admin → view_basic_stats
            INSERT INTO workspace_role_permissions (ws_role_id, permission_id)
            SELECT wr.id, p.id
            FROM workspace_roles wr
            CROSS JOIN permissions p
            WHERE wr.workspace_id IS NULL AND wr.name = 'ws_admin'
              AND p.name = 'view_basic_stats'
              AND NOT EXISTS (
                  SELECT 1 FROM workspace_role_permissions x
                  WHERE x.ws_role_id = wr.id AND x.permission_id = p.id
              );

            -- ws_admin → view_team_analytics
            INSERT INTO workspace_role_permissions (ws_role_id, permission_id)
            SELECT wr.id, p.id
            FROM workspace_roles wr
            CROSS JOIN permissions p
            WHERE wr.workspace_id IS NULL AND wr.name = 'ws_admin'
              AND p.name = 'view_team_analytics'
              AND NOT EXISTS (
                  SELECT 1 FROM workspace_role_permissions x
                  WHERE x.ws_role_id = wr.id AND x.permission_id = p.id
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
            USING permissions p
            WHERE wrp.permission_id = p.id
              AND p.name IN ('view_basic_stats', 'view_team_analytics');

            DELETE FROM permissions
            WHERE name IN ('view_basic_stats', 'view_team_analytics');

            SELECT setval(pg_get_serial_sequence('permissions', 'id'),
                          (SELECT COALESCE(MAX(id), 1) FROM permissions));
            SELECT setval(pg_get_serial_sequence('workspace_role_permissions', 'id'),
                          (SELECT COALESCE(MAX(id), 1) FROM workspace_role_permissions));
            """);
    }
}
