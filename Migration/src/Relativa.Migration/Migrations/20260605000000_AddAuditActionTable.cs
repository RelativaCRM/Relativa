using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

[DbContext(typeof(MigrationDbContext))]
[Migration("20260605000000_AddAuditActionTable")]
public partial class AddAuditActionTable : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "audit_action",
            columns: table => new
            {
                name         = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_action", x => x.name);
            });

        migrationBuilder.Sql("""
            INSERT INTO audit_action (name, display_name) VALUES
                ('entity_created',                    'Record Created'),
                ('entity_updated',                    'Record Updated'),
                ('entity_archived',                   'Record Archived'),
                ('workspace_created',                 'Workspace Created'),
                ('workspace_updated',                 'Workspace Updated'),
                ('workspace_archived',                'Workspace Archived'),
                ('workspace_member_added',            'Member Added'),
                ('workspace_member_removed',          'Member Removed'),
                ('workspace_member_role_changed',     'Member Role Changed'),
                ('workspace_settings_updated',        'Settings Updated'),
                ('workspace_role_created',            'Role Created'),
                ('workspace_role_updated',            'Role Updated'),
                ('workspace_role_archived',           'Role Archived'),
                ('organization_created',              'Organization Created'),
                ('organization_settings_updated',     'Settings Updated'),
                ('organization_member_added',         'Member Added'),
                ('organization_member_removed',       'Member Removed'),
                ('organization_member_role_changed',  'Member Role Changed'),
                ('organization_invitation_created',   'Invitation Sent'),
                ('organization_invitation_accepted',  'Invitation Accepted'),
                ('organization_invitation_cancelled', 'Invitation Cancelled'),
                ('organization_role_created',         'Role Created'),
                ('organization_role_updated',         'Role Updated'),
                ('organization_role_archived',        'Role Archived'),
                ('organization_join_request_created', 'Join Request Submitted'),
                ('organization_join_request_reviewed','Join Request Reviewed'),
                ('user_registered',                   'User Registered'),
                ('user_profile_updated',              'Profile Updated'),
                ('user_password_reset_requested',     'Password Reset Requested'),
                ('user_password_reset_completed',     'Password Reset Completed')
            ON CONFLICT (name) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "audit_action");
    }
}
