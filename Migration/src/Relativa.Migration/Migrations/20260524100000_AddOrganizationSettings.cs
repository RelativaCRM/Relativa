using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

[DbContext(typeof(MigrationDbContext))]
[Migration("20260524100000_AddOrganizationSettings")]
public partial class AddOrganizationSettings : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "organization_settings",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                workspace_id = table.Column<int>(type: "integer", nullable: false),
                high_risk_threshold = table.Column<decimal>(type: "numeric(3,2)", nullable: false, defaultValue: 0.7m),
                medium_risk_threshold = table.Column<decimal>(type: "numeric(3,2)", nullable: false, defaultValue: 0.4m)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_organization_settings", x => x.id);
                table.ForeignKey(
                    name: "fk_org_settings_workspace",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_organization_settings_workspace_id",
            table: "organization_settings",
            column: "workspace_id",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "organization_settings");
    }
}
