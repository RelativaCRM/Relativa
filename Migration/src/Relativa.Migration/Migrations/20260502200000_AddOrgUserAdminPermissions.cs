using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;

#nullable disable

namespace Relativa.Migration.Migrations;

/// <summary>
/// Adds three organization-scoped permissions for user provisioning and administration.
/// </summary>
public partial class AddOrgUserAdminPermissions : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO permissions (id, name, is_archived) VALUES
            (17, 'create_org_users', FALSE),
            (18, 'edit_other_org_users_profile', FALSE),
            (19, 'delete_org_users', FALSE);

            INSERT INTO organization_role_permissions (id, org_role_id, permission_id) VALUES
            (14, 1, 17), (15, 1, 18), (16, 1, 19),
            (17, 2, 17), (18, 2, 18), (19, 2, 19);

            SELECT setval(pg_get_serial_sequence('permissions', 'id'), (SELECT COALESCE(MAX(id),1) FROM permissions));
            SELECT setval(pg_get_serial_sequence('organization_role_permissions', 'id'), (SELECT COALESCE(MAX(id),1) FROM organization_role_permissions));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM organization_role_permissions WHERE permission_id IN (17, 18, 19);
            DELETE FROM permissions WHERE id IN (17, 18, 19);
            SELECT setval(pg_get_serial_sequence('permissions', 'id'), (SELECT COALESCE(MAX(id),1) FROM permissions));
            SELECT setval(pg_get_serial_sequence('organization_role_permissions', 'id'), (SELECT COALESCE(MAX(id),1) FROM organization_role_permissions));
            """);
    }
}
