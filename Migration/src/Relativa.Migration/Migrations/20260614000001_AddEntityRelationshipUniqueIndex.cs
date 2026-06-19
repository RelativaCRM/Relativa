using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityRelationshipUniqueIndex : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_er_source_type_target_unique",
                table: "entity_relationship",
                columns: new[] { "source_entity_id", "relationship_type_id", "target_entity_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_er_source_type_target_unique",
                table: "entity_relationship");
        }
    }
}
