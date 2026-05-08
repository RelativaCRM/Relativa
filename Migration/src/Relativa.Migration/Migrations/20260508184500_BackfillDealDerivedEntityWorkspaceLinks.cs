using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

[DbContext(typeof(MigrationDbContext))]
[Migration("20260508184500_BackfillDealDerivedEntityWorkspaceLinks")]
public partial class BackfillDealDerivedEntityWorkspaceLinks : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO entity_workspace (entity_id, workspace_id)
            SELECT er.target_entity_id, ew.workspace_id
            FROM entity_relationship er
            JOIN entity_workspace ew ON ew.entity_id = er.source_entity_id
            JOIN entity_relationship_type ert ON ert.id = er.relationship_type_id
            WHERE ert.name IN ('deal_analysis', 'deal_contract')
            ON CONFLICT (entity_id, workspace_id) DO NOTHING;
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Non-destructive data repair migration.
    }
}
