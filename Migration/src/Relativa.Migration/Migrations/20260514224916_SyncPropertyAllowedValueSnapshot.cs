using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;

#nullable disable

namespace Relativa.Migration.Migrations;

public partial class SyncPropertyAllowedValueSnapshot : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Table was created by AddPropertyAllowedValues via raw SQL which didn't update the
        // EF snapshot. This migration exists only to sync the snapshot; the table may already exist.
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS property_allowed_value (
                property_id INTEGER NOT NULL REFERENCES property(id) ON DELETE CASCADE,
                value       TEXT    NOT NULL,
                PRIMARY KEY (property_id, value)
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "property_allowed_value");
    }
}
