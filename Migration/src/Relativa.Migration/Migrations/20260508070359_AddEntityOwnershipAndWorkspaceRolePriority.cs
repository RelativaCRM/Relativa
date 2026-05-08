using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityOwnershipAndWorkspaceRolePriority : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "priority",
                table: "workspace_roles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "created_by_user_id",
                table: "entity",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE workspace_roles SET priority = 0 WHERE workspace_id IS NULL AND name = 'ws_admin';
                UPDATE workspace_roles SET priority = 2 WHERE workspace_id IS NULL AND name = 'ws_manager';
                UPDATE workspace_roles SET priority = 4 WHERE workspace_id IS NULL AND name = 'ws_analyst';
                UPDATE workspace_roles SET priority = 6 WHERE workspace_id IS NULL AND name = 'ws_member';
                UPDATE workspace_roles SET priority = 10 WHERE workspace_id IS NOT NULL AND priority IS NULL;
                UPDATE workspace_roles SET priority = 10 WHERE priority IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE entity e
                SET created_by_user_id = src.created_by_user_id
                FROM (
                    SELECT ew.entity_id, MIN(w.created_by_user_id) AS created_by_user_id
                    FROM entity_workspace ew
                    JOIN workspaces w ON w.id = ew.workspace_id
                    GROUP BY ew.entity_id
                ) src
                WHERE e.id = src.entity_id
                  AND e.created_by_user_id IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE entity
                SET created_by_user_id = (
                    SELECT id
                    FROM users
                    WHERE is_archived = FALSE
                    ORDER BY id
                    LIMIT 1
                )
                WHERE created_by_user_id IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "priority",
                table: "workspace_roles",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "created_by_user_id",
                table: "entity",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_entity_created_by_user",
                table: "entity",
                column: "created_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_entity_created_by_user",
                table: "entity",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_entity_created_by_user",
                table: "entity");

            migrationBuilder.DropIndex(
                name: "ix_entity_created_by_user",
                table: "entity");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "workspace_roles");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "entity");
        }
    }
}
