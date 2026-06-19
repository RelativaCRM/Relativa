using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class AddEpvPropertyIdIndex : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_entity_property_value_property_id",
                table: "entity_property_value",
                newName: "ix_epv_property_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "ix_epv_property_id",
                table: "entity_property_value",
                newName: "IX_entity_property_value_property_id");
        }
    }
}
