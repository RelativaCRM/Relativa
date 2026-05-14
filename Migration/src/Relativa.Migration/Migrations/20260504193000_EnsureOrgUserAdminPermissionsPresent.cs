using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

[DbContext(typeof(MigrationDbContext))]
[Migration("20260504193000_EnsureOrgUserAdminPermissionsPresent")]
public partial class EnsureOrgUserAdminPermissionsPresent : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- Ensure org user admin permission rows exist (idempotent).
            INSERT INTO permissions (id, name, is_archived)
            SELECT 17, 'create_org_users', FALSE
            WHERE NOT EXISTS (SELECT 1 FROM permissions WHERE id = 17);

            INSERT INTO permissions (id, name, is_archived)
            SELECT 18, 'edit_other_org_users_profile', FALSE
            WHERE NOT EXISTS (SELECT 1 FROM permissions WHERE id = 18);

            INSERT INTO permissions (id, name, is_archived)
            SELECT 19, 'delete_org_users', FALSE
            WHERE NOT EXISTS (SELECT 1 FROM permissions WHERE id = 19);

            -- Ensure permission names map to expected ids if rows exist but were archived.
            UPDATE permissions
            SET is_archived = FALSE
            WHERE id IN (17, 18, 19);

            -- Ensure org_owner (id=1) and org_admin (id=2) have these permissions.
            INSERT INTO organization_role_permissions (org_role_id, permission_id)
            SELECT 1, 17
            WHERE NOT EXISTS (
                SELECT 1 FROM organization_role_permissions
                WHERE org_role_id = 1 AND permission_id = 17
            );
            INSERT INTO organization_role_permissions (org_role_id, permission_id)
            SELECT 1, 18
            WHERE NOT EXISTS (
                SELECT 1 FROM organization_role_permissions
                WHERE org_role_id = 1 AND permission_id = 18
            );
            INSERT INTO organization_role_permissions (org_role_id, permission_id)
            SELECT 1, 19
            WHERE NOT EXISTS (
                SELECT 1 FROM organization_role_permissions
                WHERE org_role_id = 1 AND permission_id = 19
            );

            INSERT INTO organization_role_permissions (org_role_id, permission_id)
            SELECT 2, 17
            WHERE NOT EXISTS (
                SELECT 1 FROM organization_role_permissions
                WHERE org_role_id = 2 AND permission_id = 17
            );
            INSERT INTO organization_role_permissions (org_role_id, permission_id)
            SELECT 2, 18
            WHERE NOT EXISTS (
                SELECT 1 FROM organization_role_permissions
                WHERE org_role_id = 2 AND permission_id = 18
            );
            INSERT INTO organization_role_permissions (org_role_id, permission_id)
            SELECT 2, 19
            WHERE NOT EXISTS (
                SELECT 1 FROM organization_role_permissions
                WHERE org_role_id = 2 AND permission_id = 19
            );

            SELECT setval(pg_get_serial_sequence('permissions', 'id'),
                          (SELECT COALESCE(MAX(id), 1) FROM permissions));
            SELECT setval(pg_get_serial_sequence('organization_role_permissions', 'id'),
                          (SELECT COALESCE(MAX(id), 1) FROM organization_role_permissions));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM organization_role_permissions
            WHERE org_role_id IN (1, 2)
              AND permission_id IN (17, 18, 19);

            DELETE FROM permissions
            WHERE id IN (17, 18, 19)
              AND NOT EXISTS (
                SELECT 1
                FROM organization_role_permissions
                WHERE permission_id IN (17, 18, 19)
              );
            """);
    }
}
