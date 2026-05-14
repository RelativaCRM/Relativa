using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;

#nullable disable

namespace Relativa.Migration.Migrations;

/// <summary>
/// Prod-grade invitation system:
///
/// 1. Adds <c>org_role_id</c> column + FK to <c>organization_invitations</c> so invitations can
///    target a specific org role (defaulting legacy rows to <c>org_member</c>).
/// 2. Adds a new <c>workspace_join_requests</c> table mirroring the organization variant.
/// 3. Adds a new org-scoped permission <c>manage_ws_join_requests</c> (id 20) and grants it to
///    <c>ws_admin</c> (id 1) via <c>workspace_role_permissions</c>.
/// 4. Creates partial unique indexes on pending invitations and pending join requests to
///    prevent duplicate rows for the same (scope, email|user_id) while still allowing
///    historical/terminal rows.
/// </summary>
public partial class InvitationSystemProdGrade : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- ----------------------------------------------------------------
            -- 1. Add org_role_id to organization_invitations (backfill = org_member)
            -- ----------------------------------------------------------------
            ALTER TABLE organization_invitations
                ADD COLUMN org_role_id INTEGER;

            UPDATE organization_invitations
            SET org_role_id = (SELECT id FROM organization_roles WHERE name = 'org_member' AND organization_id IS NULL)
            WHERE org_role_id IS NULL;

            ALTER TABLE organization_invitations
                ALTER COLUMN org_role_id SET NOT NULL;

            ALTER TABLE organization_invitations
                ADD CONSTRAINT fk_oi_org_role
                FOREIGN KEY (org_role_id) REFERENCES organization_roles(id) ON DELETE RESTRICT;

            CREATE INDEX IF NOT EXISTS "IX_organization_invitations_org_role_id"
                ON organization_invitations (org_role_id);

            -- ----------------------------------------------------------------
            -- 2. workspace_join_requests table
            -- ----------------------------------------------------------------
            CREATE TABLE workspace_join_requests (
                id                    SERIAL PRIMARY KEY,
                user_id               INTEGER NOT NULL,
                workspace_id          INTEGER NOT NULL,
                message               TEXT,
                status                TEXT NOT NULL,
                created_at            TIMESTAMPTZ NOT NULL,
                reviewed_by_user_id   INTEGER,
                reviewed_at           TIMESTAMPTZ,
                CONSTRAINT fk_wjr_user        FOREIGN KEY (user_id)             REFERENCES users(id)       ON DELETE CASCADE,
                CONSTRAINT fk_wjr_workspace   FOREIGN KEY (workspace_id)        REFERENCES workspaces(id)  ON DELETE CASCADE,
                CONSTRAINT fk_wjr_reviewed_by FOREIGN KEY (reviewed_by_user_id) REFERENCES users(id)       ON DELETE SET NULL
            );

            CREATE INDEX ix_wjr_workspace_status ON workspace_join_requests (workspace_id, status);
            CREATE INDEX ix_wjr_user_status      ON workspace_join_requests (user_id,      status);
            CREATE INDEX "IX_workspace_join_requests_reviewed_by_user_id"
                ON workspace_join_requests (reviewed_by_user_id);

            -- ----------------------------------------------------------------
            -- 3. Partial unique indexes (one pending row per scope+target)
            -- ----------------------------------------------------------------
            CREATE UNIQUE INDEX ux_oi_org_email_pending
                ON organization_invitations (organization_id, lower(email))
                WHERE status = 'Pending';

            CREATE UNIQUE INDEX ux_wi_ws_email_pending
                ON workspace_invitations (workspace_id, lower(email))
                WHERE status = 'Pending';

            CREATE UNIQUE INDEX ux_ojr_org_user_pending
                ON organization_join_requests (organization_id, user_id)
                WHERE status = 'Pending';

            CREATE UNIQUE INDEX ux_wjr_ws_user_pending
                ON workspace_join_requests (workspace_id, user_id)
                WHERE status = 'Pending';

            -- ----------------------------------------------------------------
            -- 4. New workspace-scoped permission + grant to ws_admin
            -- ----------------------------------------------------------------
            INSERT INTO permissions (id, name, is_archived)
            VALUES (20, 'manage_ws_join_requests', FALSE);

            INSERT INTO workspace_role_permissions (ws_role_id, permission_id)
            VALUES (1, 20);

            SELECT setval(pg_get_serial_sequence('permissions', 'id'),
                          (SELECT COALESCE(MAX(id), 1) FROM permissions));
            SELECT setval(pg_get_serial_sequence('workspace_role_permissions', 'id'),
                          (SELECT COALESCE(MAX(id), 1) FROM workspace_role_permissions));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM workspace_role_permissions WHERE permission_id = 20;
            DELETE FROM permissions WHERE id = 20;

            DROP INDEX IF EXISTS ux_wjr_ws_user_pending;
            DROP INDEX IF EXISTS ux_ojr_org_user_pending;
            DROP INDEX IF EXISTS ux_wi_ws_email_pending;
            DROP INDEX IF EXISTS ux_oi_org_email_pending;

            DROP TABLE IF EXISTS workspace_join_requests;

            ALTER TABLE organization_invitations DROP CONSTRAINT IF EXISTS fk_oi_org_role;
            DROP INDEX IF EXISTS "IX_organization_invitations_org_role_id";
            ALTER TABLE organization_invitations DROP COLUMN IF EXISTS org_role_id;

            SELECT setval(pg_get_serial_sequence('permissions', 'id'),
                          (SELECT COALESCE(MAX(id), 1) FROM permissions));
            SELECT setval(pg_get_serial_sequence('workspace_role_permissions', 'id'),
                          (SELECT COALESCE(MAX(id), 1) FROM workspace_role_permissions));
            """);
    }
}
