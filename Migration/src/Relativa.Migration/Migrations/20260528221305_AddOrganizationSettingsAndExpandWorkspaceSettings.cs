using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationSettingsAndExpandWorkspaceSettings : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The previous rename migration renamed the table organization_settings → workspace_settings
            // but left the PK constraint name unchanged. Rename it now so we can reuse the name
            // for the new organization_settings table created below.
            migrationBuilder.Sql(@"ALTER INDEX ""PK_organization_settings"" RENAME TO ""PK_workspace_settings"";");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "workspace_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "risk_scoring_enabled",
                table: "workspace_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "organization_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    organization_id = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    join_policy = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false, defaultValue: "open"),
                    default_org_role_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_settings", x => x.id);
                    table.CheckConstraint("ck_org_settings_join_policy", "join_policy IN ('open', 'invite_only')");
                    table.ForeignKey(
                        name: "fk_org_settings_default_org_role",
                        column: x => x.default_org_role_id,
                        principalTable: "organization_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_org_settings_organization",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_org_settings_organization_id",
                table: "organization_settings",
                column: "organization_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_settings_default_org_role_id",
                table: "organization_settings",
                column: "default_org_role_id");

            // Backfill settings rows for every organization that was created before this migration.
            migrationBuilder.Sql(
                """
                INSERT INTO organization_settings (organization_id, description, join_policy)
                SELECT id, NULL, 'open'
                FROM organizations
                ON CONFLICT (organization_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_settings");

            migrationBuilder.DropColumn(
                name: "description",
                table: "workspace_settings");

            migrationBuilder.DropColumn(
                name: "risk_scoring_enabled",
                table: "workspace_settings");

            // Restore the PK name that Up() renamed.
            migrationBuilder.Sql(@"ALTER INDEX ""PK_workspace_settings"" RENAME TO ""PK_organization_settings"";");
        }
    }
}
