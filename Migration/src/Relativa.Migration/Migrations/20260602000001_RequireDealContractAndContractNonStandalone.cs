using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

[DbContext(typeof(MigrationDbContext))]
[Migration("20260602000001_RequireDealContractAndContractNonStandalone")]
public partial class RequireDealContractAndContractNonStandalone : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE entity_relationship_type
            SET is_required = TRUE
            WHERE name = 'deal_contract';

            UPDATE entity_type
            SET is_standalone = FALSE
            WHERE name = 'contract';
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE entity_relationship_type
            SET is_required = FALSE
            WHERE name = 'deal_contract';

            UPDATE entity_type
            SET is_standalone = TRUE
            WHERE name = 'contract';
            """
        );
    }
}
