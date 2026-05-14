using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class RelationshipCardinalityAndSeeds : EfMigration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "relationship_cardinality",
                table: "entity_relationship_type",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "many_to_one");

            migrationBuilder.Sql(
                """
                UPDATE entity_relationship_type
                SET relationship_cardinality = 'many_to_one'
                WHERE relationship_cardinality = '' OR relationship_cardinality IS NULL;

                UPDATE entity_relationship_type
                SET is_required = FALSE
                WHERE name = 'deal_analysis';

                INSERT INTO entity_relationship_type (name, source_entity_type_id, target_entity_type_id, is_required, relationship_cardinality)
                SELECT 'contract_deal', ct.id, dt.id, TRUE, 'many_to_one'
                FROM entity_type ct
                CROSS JOIN entity_type dt
                WHERE ct.name = 'contract' AND dt.name = 'deal'
                  AND NOT EXISTS (
                    SELECT 1 FROM entity_relationship_type ert
                    WHERE ert.name = 'contract_deal'
                      AND ert.source_entity_type_id = ct.id
                      AND ert.target_entity_type_id = dt.id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM entity_relationship_type WHERE name = 'contract_deal';
                """);

            migrationBuilder.DropColumn(
                name: "relationship_cardinality",
                table: "entity_relationship_type");
        }
    }
}
