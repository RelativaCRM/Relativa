using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

/// <summary>
/// Sets default priority for custom org roles that were still NULL, then enforces NOT NULL on <c>priority</c>.
/// </summary>
[DbContext(typeof(MigrationDbContext))]
[Migration("20260508120100_BackfillCustomOrganizationRolePriority")]
public partial class BackfillCustomOrganizationRolePriority : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE organization_roles
            SET priority = 10
            WHERE organization_id IS NOT NULL AND priority IS NULL;
            """);

        migrationBuilder.Sql(
            """
            UPDATE organization_roles SET priority = 0 WHERE organization_id IS NULL AND name = 'org_owner' AND priority IS NULL;
            UPDATE organization_roles SET priority = 1 WHERE organization_id IS NULL AND name = 'org_admin' AND priority IS NULL;
            UPDATE organization_roles SET priority = 6 WHERE organization_id IS NULL AND name = 'org_member' AND priority IS NULL;
            """);

        migrationBuilder.AlterColumn<int>(
            name: "priority",
            table: "organization_roles",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(
            name: "priority",
            table: "organization_roles",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: false);
    }
}
