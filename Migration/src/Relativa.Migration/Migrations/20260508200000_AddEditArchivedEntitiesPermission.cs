using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

/// <summary>
/// Adds workspace permission <c>edit_archived_entities</c> and grants it only to the
/// system <c>ws_admin</c> role.
/// </summary>
[DbContext(typeof(MigrationDbContext))]
[Migration("20260508200000_AddEditArchivedEntitiesPermission")]
public partial class AddEditArchivedEntitiesPermission : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO permissions (name, is_archived)
            SELECT 'edit_archived_entities', FALSE
            WHERE NOT EXISTS (
                SELECT 1 FROM permissions WHERE name = 'edit_archived_entities'
            );

            SELECT setval(pg_get_serial_sequence('permissions', 'id'),
                          (SELECT COALESCE(MAX(id), 1) FROM permissions));

            INSERT INTO workspace_role_permissions (ws_role_id, permission_id)
            SELECT wr.id, p.id
            FROM workspace_roles wr
            CROSS JOIN permissions p
            WHERE wr.workspace_id IS NULL
              AND wr.name = 'ws_admin'
              AND p.name = 'edit_archived_entities'
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
              AND p.name = 'edit_archived_entities';

            DELETE FROM permissions
            WHERE name = 'edit_archived_entities';

            SELECT setval(pg_get_serial_sequence('permissions', 'id'),
                          (SELECT COALESCE(MAX(id), 1) FROM permissions));
            SELECT setval(pg_get_serial_sequence('workspace_role_permissions', 'id'),
                          (SELECT COALESCE(MAX(id), 1) FROM workspace_role_permissions));
            """);
    }
}
