using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyIsReadonly : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_readonly",
                table: "property",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE property
                SET is_readonly = TRUE
                WHERE organization_id IS NULL
                  AND name IN (
                    'days_since_created',
                    'stage_encoded',
                    'num_interactions',
                    'days_since_last_contact',
                    'num_open_deals',
                    'avg_deal_value',
                    'source_updated_at',
                    'calculated_at'
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_readonly",
                table: "property");
        }
    }
}
