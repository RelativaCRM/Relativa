using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;

#nullable disable

namespace Relativa.Migration.Migrations;

/// <summary>
/// Repairs databases where <c>organization_invitations</c> never received <c>org_role_id</c>
/// (e.g. migration history out of sync). Safe when the column already exists (matches InvitationSystemProdGrade).
/// </summary>
public partial class EnsureOrganizationInvitationOrgRoleColumn : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'organization_invitations'
                      AND column_name = 'org_role_id'
                ) THEN
                    ALTER TABLE organization_invitations ADD COLUMN org_role_id INTEGER;

                    UPDATE organization_invitations
                    SET org_role_id = (
                        SELECT id FROM organization_roles
                        WHERE name = 'org_member' AND organization_id IS NULL
                        LIMIT 1
                    )
                    WHERE org_role_id IS NULL;

                    ALTER TABLE organization_invitations
                        ALTER COLUMN org_role_id SET NOT NULL;

                    ALTER TABLE organization_invitations
                        ADD CONSTRAINT fk_oi_org_role
                        FOREIGN KEY (org_role_id) REFERENCES organization_roles(id) ON DELETE RESTRICT;

                    CREATE INDEX IF NOT EXISTS "IX_organization_invitations_org_role_id"
                        ON organization_invitations (org_role_id);

                    CREATE UNIQUE INDEX IF NOT EXISTS ux_oi_org_email_pending
                        ON organization_invitations (organization_id, lower(email))
                        WHERE status = 'Pending';
                END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new InvalidOperationException(
            "This migration cannot be reverted automatically (org_role_id repair is idempotent forward-only).");
    }
}
