using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

/// <summary>
/// Adds nullable <c>priority</c> and backfills system roles only (custom rows stay NULL for migration B).
/// Tiers: org_owner=0, org_admin=1, org_member=6 (lower = stronger).
/// </summary>
[DbContext(typeof(MigrationDbContext))]
[Migration("20260508120000_AddOrganizationRolePriority")]
public partial class AddOrganizationRolePriority : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "priority",
            table: "organization_roles",
            type: "integer",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE organization_roles SET priority = 0 WHERE organization_id IS NULL AND name = 'org_owner';
            UPDATE organization_roles SET priority = 1 WHERE organization_id IS NULL AND name = 'org_admin';
            UPDATE organization_roles SET priority = 6 WHERE organization_id IS NULL AND name = 'org_member';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "priority",
            table: "organization_roles");
    }
}
