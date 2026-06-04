using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingPerformanceIndexes : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Give the auto-generated FK index on target_entity_id a canonical name
            migrationBuilder.RenameIndex(
                name: "IX_entity_relationship_target_entity_id",
                table: "entity_relationship",
                newName: "ix_er_target_entity_id");

            // Add a workspace-first index so workspace-scoped queries don't full-scan entity_workspace
            migrationBuilder.CreateIndex(
                name: "ix_ew_workspace_id",
                table: "entity_workspace",
                column: "workspace_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ew_workspace_id",
                table: "entity_workspace");

            migrationBuilder.RenameIndex(
                name: "ix_er_target_entity_id",
                table: "entity_relationship",
                newName: "IX_entity_relationship_target_entity_id");
        }
    }
}
