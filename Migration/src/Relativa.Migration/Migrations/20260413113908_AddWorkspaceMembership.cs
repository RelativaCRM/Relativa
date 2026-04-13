using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceMembership : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_role",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_roles_name",
                table: "roles");

            migrationBuilder.AddColumn<int>(
                name: "created_by_user_id",
                table: "workspaces",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "role_id",
                table: "users",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "workspace_id",
                table: "roles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "workspace_invitations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    workspace_id = table.Column<int>(type: "integer", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    invited_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_invitations", x => x.id);
                    table.ForeignKey(
                        name: "fk_wi_invited_by",
                        column: x => x.invited_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_wi_role",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_wi_workspace",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_members",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    workspace_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_wm_role",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_wm_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_wm_workspace",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_created_by_user_id",
                table: "workspaces",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_roles_name_workspace",
                table: "roles",
                columns: new[] { "name", "workspace_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_workspace_id",
                table: "roles",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_invited_by_user_id",
                table: "workspace_invitations",
                column: "invited_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_role_id",
                table: "workspace_invitations",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_invitations_token",
                table: "workspace_invitations",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_workspace_id",
                table: "workspace_invitations",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_members_role_id",
                table: "workspace_members",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_members_user_workspace",
                table: "workspace_members",
                columns: new[] { "user_id", "workspace_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspace_members_workspace_id",
                table: "workspace_members",
                column: "workspace_id");

            migrationBuilder.AddForeignKey(
                name: "fk_roles_workspace",
                table: "roles",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_users_role",
                table: "users",
                column: "role_id",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_workspaces_created_by",
                table: "workspaces",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
-- Backfill created_by_user_id for existing workspaces (admin user = 1)
UPDATE workspaces SET created_by_user_id = 1 WHERE created_by_user_id = 0;

-- Create workspace_member rows for existing seeded users
-- User 1 (admin) in workspace 1 with admin role (role_id=1)
-- User 2 (sales_manager) in workspace 1 with sales_manager role (role_id=2)
-- User 3 (analyst) in workspace 1 with analyst role (role_id=3)
INSERT INTO workspace_members (user_id, workspace_id, role_id, joined_at, is_archived) VALUES
(1, 1, 1, CURRENT_TIMESTAMP, FALSE),
(2, 1, 2, CURRENT_TIMESTAMP, FALSE),
(3, 1, 3, CURRENT_TIMESTAMP, FALSE);

SELECT setval(pg_get_serial_sequence('workspace_members', 'id'), coalesce(max(id), 1), max(id) IS NOT null) FROM workspace_members;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_roles_workspace",
                table: "roles");

            migrationBuilder.DropForeignKey(
                name: "fk_users_role",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "fk_workspaces_created_by",
                table: "workspaces");

            migrationBuilder.DropTable(
                name: "workspace_invitations");

            migrationBuilder.DropTable(
                name: "workspace_members");

            migrationBuilder.DropIndex(
                name: "IX_workspaces_created_by_user_id",
                table: "workspaces");

            migrationBuilder.DropIndex(
                name: "ix_roles_name_workspace",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "IX_roles_workspace_id",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "workspace_id",
                table: "roles");

            migrationBuilder.AlterColumn<int>(
                name: "role_id",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_name",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_users_role",
                table: "users",
                column: "role_id",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
