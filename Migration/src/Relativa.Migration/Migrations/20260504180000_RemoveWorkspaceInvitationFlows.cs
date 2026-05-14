using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

/// <summary>
/// Removes workspace-scoped invitations and join requests. Drops related tables and
/// workspace permissions <c>invite_to_workspace</c> (id 9) and <c>manage_ws_join_requests</c> (id 20).
/// Adds organization permission <c>manage_org_workspace_members</c> (id 21) for org_owner and org_admin.
/// </summary>
[DbContext(typeof(MigrationDbContext))]
[Migration("20260504180000_RemoveWorkspaceInvitationFlows")]
public partial class RemoveWorkspaceInvitationFlows : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- Drop partial unique index on workspace invitations (if present)
            DROP INDEX IF EXISTS ux_wi_ws_email_pending;

            DROP INDEX IF EXISTS ix_wi_email_status;
            DROP INDEX IF EXISTS ix_wi_workspace_status;
            DROP INDEX IF EXISTS "IX_workspace_invitations_invited_by_user_id";
            DROP INDEX IF EXISTS "IX_workspace_invitations_workspace_id";
            DROP INDEX IF EXISTS "IX_workspace_invitations_ws_role_id";
            DROP INDEX IF EXISTS ix_workspace_invitations_token;

            DROP INDEX IF EXISTS ux_wjr_ws_user_pending;
            DROP INDEX IF EXISTS ix_wjr_workspace_status;
            DROP INDEX IF EXISTS ix_wjr_user_status;
            DROP INDEX IF EXISTS "IX_workspace_join_requests_reviewed_by_user_id";

            -- Remove workspace permission grants before deleting permission rows
            DELETE FROM workspace_role_permissions WHERE permission_id IN (9, 20);
            DELETE FROM permissions WHERE id IN (9, 20);

            INSERT INTO permissions (id, name, is_archived)
            VALUES (21, 'manage_org_workspace_members', FALSE);

            INSERT INTO organization_role_permissions (org_role_id, permission_id)
            SELECT r.id, 21
            FROM organization_roles r
            WHERE r.organization_id IS NULL
              AND r.name IN ('org_owner', 'org_admin');

            DROP TABLE IF EXISTS workspace_join_requests;
            DROP TABLE IF EXISTS workspace_invitations;

            SELECT setval(pg_get_serial_sequence('permissions', 'id'),
                          (SELECT COALESCE(MAX(id), 1) FROM permissions));
            SELECT setval(pg_get_serial_sequence('organization_role_permissions', 'id'),
                          (SELECT COALESCE(MAX(id), 1) FROM organization_role_permissions));
            SELECT setval(pg_get_serial_sequence('workspace_role_permissions', 'id'),
                          (SELECT COALESCE(MAX(id), 1) FROM workspace_role_permissions));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new InvalidOperationException(
            "This migration cannot be reverted automatically (workspace invitation tables and permission ids were removed).");
    }
}
