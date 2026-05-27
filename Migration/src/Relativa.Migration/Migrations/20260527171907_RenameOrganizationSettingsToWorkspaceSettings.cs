using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class RenameOrganizationSettingsToWorkspaceSettings : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_org_settings_workspace",
                table: "organization_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_org_settings_high_risk",
                table: "organization_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_org_settings_medium_risk",
                table: "organization_settings");

            migrationBuilder.RenameTable(
                name: "organization_settings",
                newName: "workspace_settings");

            migrationBuilder.RenameIndex(
                name: "ix_organization_settings_workspace_id",
                table: "workspace_settings",
                newName: "ix_workspace_settings_workspace_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_workspace_settings_high_risk",
                table: "workspace_settings",
                sql: "high_risk_threshold BETWEEN 0.00 AND 1.00");

            migrationBuilder.AddCheckConstraint(
                name: "ck_workspace_settings_medium_risk",
                table: "workspace_settings",
                sql: "medium_risk_threshold BETWEEN 0.00 AND 1.00 AND medium_risk_threshold < high_risk_threshold");

            migrationBuilder.AddForeignKey(
                name: "fk_workspace_settings_workspace",
                table: "workspace_settings",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_workspace_settings_workspace",
                table: "workspace_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_workspace_settings_high_risk",
                table: "workspace_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_workspace_settings_medium_risk",
                table: "workspace_settings");

            migrationBuilder.RenameTable(
                name: "workspace_settings",
                newName: "organization_settings");

            migrationBuilder.RenameIndex(
                name: "ix_workspace_settings_workspace_id",
                table: "organization_settings",
                newName: "ix_organization_settings_workspace_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_org_settings_high_risk",
                table: "organization_settings",
                sql: "high_risk_threshold BETWEEN 0.00 AND 1.00");

            migrationBuilder.AddCheckConstraint(
                name: "ck_org_settings_medium_risk",
                table: "organization_settings",
                sql: "medium_risk_threshold BETWEEN 0.00 AND 1.00 AND medium_risk_threshold < high_risk_threshold");

            migrationBuilder.AddForeignKey(
                name: "fk_org_settings_workspace",
                table: "organization_settings",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
