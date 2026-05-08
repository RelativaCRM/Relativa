using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class SplitEntityWorkspacePermissions : EfMigration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO permissions (id, name, is_archived)
                SELECT 17, 'edit_entities', FALSE
                WHERE NOT EXISTS (SELECT 1 FROM permissions WHERE id = 17);

                INSERT INTO permissions (id, name, is_archived)
                SELECT 18, 'delete_entities', FALSE
                WHERE NOT EXISTS (SELECT 1 FROM permissions WHERE id = 18);

                UPDATE permissions SET name = 'create_entities' WHERE id = 14 AND name = 'manage_entities';

                INSERT INTO workspace_role_permissions (ws_role_id, permission_id)
                SELECT DISTINCT wrp.ws_role_id, 17
                FROM workspace_role_permissions wrp
                WHERE wrp.permission_id = 14
                  AND NOT EXISTS (
                    SELECT 1 FROM workspace_role_permissions x
                    WHERE x.ws_role_id = wrp.ws_role_id AND x.permission_id = 17);

                INSERT INTO workspace_role_permissions (ws_role_id, permission_id)
                SELECT DISTINCT wrp.ws_role_id, 18
                FROM workspace_role_permissions wrp
                WHERE wrp.permission_id = 14
                  AND NOT EXISTS (
                    SELECT 1 FROM workspace_role_permissions x
                    WHERE x.ws_role_id = wrp.ws_role_id AND x.permission_id = 18);

                SELECT setval(pg_get_serial_sequence('workspace_role_permissions', 'id'),
                  (SELECT COALESCE(MAX(id), 1) FROM workspace_role_permissions));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM workspace_role_permissions WHERE permission_id IN (17, 18);
                DELETE FROM permissions WHERE id IN (17, 18);
                UPDATE permissions SET name = 'manage_entities' WHERE id = 14 AND name = 'create_entities';

                SELECT setval(pg_get_serial_sequence('workspace_role_permissions', 'id'),
                  (SELECT COALESCE(MAX(id), 1) FROM workspace_role_permissions));
                """);
        }
    }
}
