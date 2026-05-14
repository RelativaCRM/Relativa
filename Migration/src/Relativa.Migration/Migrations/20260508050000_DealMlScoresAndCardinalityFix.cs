using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

/// <summary>
/// Adds <c>churn_score</c> system property bound to the <c>deal</c> entity type, marks both
/// <c>closure_score</c> and <c>churn_score</c> as readonly (ML-derived, not user-editable),
/// and corrects the deal-side relationship cardinalities so the schema reflects reality:
/// a deal owns many contracts (<c>one_to_many</c>) and exactly one analysis (<c>one_to_one</c>).
/// </summary>
[DbContext(typeof(MigrationDbContext))]
[Migration("20260508050000_DealMlScoresAndCardinalityFix")]
public partial class DealMlScoresAndCardinalityFix : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                v_deal_type_id int;
                v_prop_churn_id int;
            BEGIN
                SELECT id INTO v_deal_type_id FROM entity_type WHERE name = 'deal' LIMIT 1;

                INSERT INTO property (name, data_type, organization_id, is_readonly)
                SELECT 'churn_score', 'Decimal', NULL, TRUE
                WHERE NOT EXISTS (
                    SELECT 1 FROM property
                    WHERE name = 'churn_score' AND organization_id IS NULL
                );

                SELECT id INTO v_prop_churn_id
                FROM property
                WHERE name = 'churn_score' AND organization_id IS NULL
                LIMIT 1;

                IF v_deal_type_id IS NOT NULL AND v_prop_churn_id IS NOT NULL THEN
                    INSERT INTO entity_type_property (entity_type_id, property_id, is_required)
                    SELECT v_deal_type_id, v_prop_churn_id, FALSE
                    WHERE NOT EXISTS (
                        SELECT 1 FROM entity_type_property
                        WHERE entity_type_id = v_deal_type_id
                          AND property_id = v_prop_churn_id
                    );
                END IF;

                UPDATE property
                SET is_readonly = TRUE
                WHERE organization_id IS NULL
                  AND name IN ('closure_score', 'churn_score');

                UPDATE entity_relationship_type
                SET relationship_cardinality = 'one_to_many'
                WHERE name = 'deal_contract';

                UPDATE entity_relationship_type
                SET relationship_cardinality = 'one_to_one'
                WHERE name = 'deal_analysis';
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                v_deal_type_id int;
                v_prop_churn_id int;
            BEGIN
                UPDATE entity_relationship_type
                SET relationship_cardinality = 'many_to_one'
                WHERE name IN ('deal_contract', 'deal_analysis');

                UPDATE property
                SET is_readonly = FALSE
                WHERE organization_id IS NULL
                  AND name = 'closure_score';

                SELECT id INTO v_deal_type_id FROM entity_type WHERE name = 'deal' LIMIT 1;
                SELECT id INTO v_prop_churn_id FROM property WHERE name = 'churn_score' AND organization_id IS NULL LIMIT 1;

                IF v_deal_type_id IS NOT NULL AND v_prop_churn_id IS NOT NULL THEN
                    DELETE FROM entity_type_property
                    WHERE entity_type_id = v_deal_type_id
                      AND property_id = v_prop_churn_id;
                END IF;

                IF v_prop_churn_id IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM entity_property_value WHERE property_id = v_prop_churn_id
                   )
                   AND NOT EXISTS (
                       SELECT 1 FROM entity_type_property WHERE property_id = v_prop_churn_id
                   ) THEN
                    DELETE FROM property WHERE id = v_prop_churn_id;
                END IF;
            END $$;
            """);
    }
}
