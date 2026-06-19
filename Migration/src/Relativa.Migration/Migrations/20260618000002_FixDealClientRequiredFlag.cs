using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

[DbContext(typeof(MigrationDbContext))]
[Migration("20260618000002_FixDealClientRequiredFlag")]
public partial class FixDealClientRequiredFlag : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // deal_client.is_required=TRUE blocks clients from removing linked deals because
        // each deal typically has exactly one client, so remaining<=1 always fires.
        // Relaxing this flag allows free management from both sides of the relationship.
        migrationBuilder.Sql(
            """
            UPDATE entity_relationship_type
            SET    is_required = FALSE
            WHERE  name = 'deal_client';
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE entity_relationship_type
            SET    is_required = TRUE
            WHERE  name = 'deal_client';
            """
        );
    }
}
