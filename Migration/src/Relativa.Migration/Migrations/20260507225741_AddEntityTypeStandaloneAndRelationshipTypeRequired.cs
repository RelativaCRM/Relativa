using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityTypeStandaloneAndRelationshipTypeRequired : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_standalone",
                table: "entity_type",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_required",
                table: "entity_relationship_type",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE entity_type SET is_standalone = FALSE WHERE name = 'deal_analysis';
                UPDATE entity_relationship_type SET is_required = TRUE WHERE name = 'deal_client';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_standalone",
                table: "entity_type");

            migrationBuilder.DropColumn(
                name: "is_required",
                table: "entity_relationship_type");
        }
    }
}
