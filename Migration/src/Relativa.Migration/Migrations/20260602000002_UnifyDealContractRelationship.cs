using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

[DbContext(typeof(MigrationDbContext))]
[Migration("20260602000002_UnifyDealContractRelationship")]
public partial class UnifyDealContractRelationship : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            -- Ensure contract_deal links exist for every deal_contract relationship
            -- (data may have been created with both types; fill any gaps before dropping deal_contract)
            INSERT INTO entity_relationship (relationship_type_id, source_entity_id, target_entity_id)
            SELECT
                cdt.id,
                dc.target_entity_id,   -- contract was target in deal_contract, becomes source
                dc.source_entity_id    -- deal was source in deal_contract, becomes target
            FROM entity_relationship dc
            JOIN entity_relationship_type dct ON dc.relationship_type_id = dct.id AND dct.name = 'deal_contract'
            JOIN entity_relationship_type cdt ON cdt.name = 'contract_deal'
            WHERE NOT EXISTS (
                SELECT 1 FROM entity_relationship existing
                WHERE existing.relationship_type_id = cdt.id
                  AND existing.source_entity_id = dc.target_entity_id
                  AND existing.target_entity_id = dc.source_entity_id
            );

            -- Remove all deal_contract relationship rows
            DELETE FROM entity_relationship
            WHERE relationship_type_id IN (
                SELECT id FROM entity_relationship_type WHERE name = 'deal_contract'
            );

            -- Remove the deal_contract relationship type itself
            DELETE FROM entity_relationship_type WHERE name = 'deal_contract';
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            -- Restore the deal_contract relationship type
            INSERT INTO entity_relationship_type (name, source_entity_type_id, target_entity_type_id, is_required, relationship_cardinality)
            SELECT 'deal_contract', dt.id, ct.id, TRUE, 'many_to_one'
            FROM entity_type dt
            CROSS JOIN entity_type ct
            WHERE dt.name = 'deal' AND ct.name = 'contract'
              AND NOT EXISTS (
                SELECT 1 FROM entity_relationship_type WHERE name = 'deal_contract'
              );

            -- Recreate deal_contract rows from the surviving contract_deal data
            INSERT INTO entity_relationship (relationship_type_id, source_entity_id, target_entity_id)
            SELECT dct.id, cd.target_entity_id, cd.source_entity_id
            FROM entity_relationship cd
            JOIN entity_relationship_type cdt ON cd.relationship_type_id = cdt.id AND cdt.name = 'contract_deal'
            JOIN entity_relationship_type dct ON dct.name = 'deal_contract';
            """
        );
    }
}
