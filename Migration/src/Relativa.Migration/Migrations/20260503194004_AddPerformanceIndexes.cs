using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workspace_invitations_workspace_id",
                table: "workspace_invitations");

            migrationBuilder.DropIndex(
                name: "IX_workspace_audit_log_changed_by",
                table: "workspace_audit_log");

            migrationBuilder.DropIndex(
                name: "ix_workspace_audit_log_workspace_id",
                table: "workspace_audit_log");

            migrationBuilder.DropIndex(
                name: "IX_user_role_workspace_workspace_id",
                table: "user_role_workspace");

            migrationBuilder.DropIndex(
                name: "IX_user_role_organization_organization_id",
                table: "user_role_organization");

            migrationBuilder.DropIndex(
                name: "IX_user_audit_log_changed_by",
                table: "user_audit_log");

            migrationBuilder.DropIndex(
                name: "ix_user_audit_log_target_user_id",
                table: "user_audit_log");

            migrationBuilder.DropIndex(
                name: "IX_organization_join_requests_organization_id",
                table: "organization_join_requests");

            migrationBuilder.DropIndex(
                name: "IX_organization_join_requests_user_id",
                table: "organization_join_requests");

            migrationBuilder.DropIndex(
                name: "IX_organization_invitations_organization_id",
                table: "organization_invitations");

            migrationBuilder.DropIndex(
                name: "IX_organization_audit_log_changed_by",
                table: "organization_audit_log");

            migrationBuilder.DropIndex(
                name: "ix_organization_audit_log_organization_id",
                table: "organization_audit_log");

            migrationBuilder.DropIndex(
                name: "IX_entity_workspace_entity_id",
                table: "entity_workspace");

            migrationBuilder.DropIndex(
                name: "ix_epv_entity_id",
                table: "entity_property_value");

            migrationBuilder.DropIndex(
                name: "IX_entity_audit_log_changed_by",
                table: "entity_audit_log");

            migrationBuilder.DropIndex(
                name: "ix_entity_audit_log_entity_id",
                table: "entity_audit_log");

            migrationBuilder.DropIndex(
                name: "ix_audit_outbox_published_at_utc",
                table: "audit_outbox");

            migrationBuilder.RenameIndex(
                name: "ix_property_organization_id",
                table: "property",
                newName: "IX_property_organization_id");

            migrationBuilder.RenameIndex(
                name: "ix_epv_property_id",
                table: "entity_property_value",
                newName: "IX_entity_property_value_property_id");

            migrationBuilder.CreateIndex(
                name: "ix_wi_email_status",
                table: "workspace_invitations",
                columns: new[] { "email", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_wi_workspace_status",
                table: "workspace_invitations",
                columns: new[] { "workspace_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_wal_changed_by_changed_at",
                table: "workspace_audit_log",
                columns: new[] { "changed_by", "changed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_wal_workspace_changed_at",
                table: "workspace_audit_log",
                columns: new[] { "workspace_id", "changed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_urw_workspace_active",
                table: "user_role_workspace",
                columns: new[] { "workspace_id", "is_archived" });

            migrationBuilder.CreateIndex(
                name: "ix_uro_org_active",
                table: "user_role_organization",
                columns: new[] { "organization_id", "is_archived" });

            migrationBuilder.CreateIndex(
                name: "ix_ual_changed_by_changed_at",
                table: "user_audit_log",
                columns: new[] { "changed_by", "changed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_ual_target_user_changed_at",
                table: "user_audit_log",
                columns: new[] { "target_user_id", "changed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_ojr_org_status",
                table: "organization_join_requests",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_ojr_user_status",
                table: "organization_join_requests",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_oi_email_status",
                table: "organization_invitations",
                columns: new[] { "email", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_oi_org_status",
                table: "organization_invitations",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_oal_changed_by_changed_at",
                table: "organization_audit_log",
                columns: new[] { "changed_by", "changed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_oal_organization_changed_at",
                table: "organization_audit_log",
                columns: new[] { "organization_id", "changed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_ew_entity_workspace",
                table: "entity_workspace",
                columns: new[] { "entity_id", "workspace_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_eal_changed_by_changed_at",
                table: "entity_audit_log",
                columns: new[] { "changed_by", "changed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_eal_entity_changed_at",
                table: "entity_audit_log",
                columns: new[] { "entity_id", "changed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_outbox_pending",
                table: "audit_outbox",
                columns: new[] { "published_at_utc", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_wi_email_status",
                table: "workspace_invitations");

            migrationBuilder.DropIndex(
                name: "ix_wi_workspace_status",
                table: "workspace_invitations");

            migrationBuilder.DropIndex(
                name: "ix_wal_changed_by_changed_at",
                table: "workspace_audit_log");

            migrationBuilder.DropIndex(
                name: "ix_wal_workspace_changed_at",
                table: "workspace_audit_log");

            migrationBuilder.DropIndex(
                name: "ix_urw_workspace_active",
                table: "user_role_workspace");

            migrationBuilder.DropIndex(
                name: "ix_uro_org_active",
                table: "user_role_organization");

            migrationBuilder.DropIndex(
                name: "ix_ual_changed_by_changed_at",
                table: "user_audit_log");

            migrationBuilder.DropIndex(
                name: "ix_ual_target_user_changed_at",
                table: "user_audit_log");

            migrationBuilder.DropIndex(
                name: "ix_ojr_org_status",
                table: "organization_join_requests");

            migrationBuilder.DropIndex(
                name: "ix_ojr_user_status",
                table: "organization_join_requests");

            migrationBuilder.DropIndex(
                name: "ix_oi_email_status",
                table: "organization_invitations");

            migrationBuilder.DropIndex(
                name: "ix_oi_org_status",
                table: "organization_invitations");

            migrationBuilder.DropIndex(
                name: "ix_oal_changed_by_changed_at",
                table: "organization_audit_log");

            migrationBuilder.DropIndex(
                name: "ix_oal_organization_changed_at",
                table: "organization_audit_log");

            migrationBuilder.DropIndex(
                name: "ix_ew_entity_workspace",
                table: "entity_workspace");

            migrationBuilder.DropIndex(
                name: "ix_eal_changed_by_changed_at",
                table: "entity_audit_log");

            migrationBuilder.DropIndex(
                name: "ix_eal_entity_changed_at",
                table: "entity_audit_log");

            migrationBuilder.DropIndex(
                name: "ix_audit_outbox_pending",
                table: "audit_outbox");

            migrationBuilder.RenameIndex(
                name: "IX_property_organization_id",
                table: "property",
                newName: "ix_property_organization_id");

            migrationBuilder.RenameIndex(
                name: "IX_entity_property_value_property_id",
                table: "entity_property_value",
                newName: "ix_epv_property_id");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_workspace_id",
                table: "workspace_invitations",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_audit_log_changed_by",
                table: "workspace_audit_log",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_audit_log_workspace_id",
                table: "workspace_audit_log",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_role_workspace_workspace_id",
                table: "user_role_workspace",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_role_organization_organization_id",
                table: "user_role_organization",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_audit_log_changed_by",
                table: "user_audit_log",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_user_audit_log_target_user_id",
                table: "user_audit_log",
                column: "target_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_join_requests_organization_id",
                table: "organization_join_requests",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_join_requests_user_id",
                table: "organization_join_requests",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_invitations_organization_id",
                table: "organization_invitations",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_audit_log_changed_by",
                table: "organization_audit_log",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_organization_audit_log_organization_id",
                table: "organization_audit_log",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_entity_workspace_entity_id",
                table: "entity_workspace",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_epv_entity_id",
                table: "entity_property_value",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "IX_entity_audit_log_changed_by",
                table: "entity_audit_log",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_entity_audit_log_entity_id",
                table: "entity_audit_log",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_outbox_published_at_utc",
                table: "audit_outbox",
                column: "published_at_utc");
        }
    }
}
