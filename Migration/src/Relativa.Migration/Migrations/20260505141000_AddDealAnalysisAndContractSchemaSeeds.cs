using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

[DbContext(typeof(MigrationDbContext))]
[Migration("20260505141000_AddDealAnalysisAndContractSchemaSeeds")]
public partial class AddDealAnalysisAndContractSchemaSeeds : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                v_deal_type_id int;
                v_client_type_id int;
                v_deal_analysis_type_id int;
                v_contract_type_id int;
                v_rel_deal_analysis_id int;
                v_rel_deal_contract_id int;
            BEGIN
                SELECT id INTO v_deal_type_id FROM entity_type WHERE name = 'deal' LIMIT 1;
                SELECT id INTO v_client_type_id FROM entity_type WHERE name = 'client' LIMIT 1;

                IF v_deal_type_id IS NULL OR v_client_type_id IS NULL THEN
                    RAISE EXCEPTION 'Required entity types not found (deal/client).';
                END IF;

                INSERT INTO entity_type (name)
                SELECT 'deal_analysis'
                WHERE NOT EXISTS (SELECT 1 FROM entity_type WHERE name = 'deal_analysis');

                INSERT INTO entity_type (name)
                SELECT 'contract'
                WHERE NOT EXISTS (SELECT 1 FROM entity_type WHERE name = 'contract');

                SELECT id INTO v_deal_analysis_type_id FROM entity_type WHERE name = 'deal_analysis' LIMIT 1;
                SELECT id INTO v_contract_type_id FROM entity_type WHERE name = 'contract' LIMIT 1;

                INSERT INTO property (name, data_type, organization_id)
                SELECT p_name, p_type, NULL
                FROM (VALUES
                    ('created_at', 'Date'),
                    ('status', 'String'),
                    ('days_since_created', 'Int'),
                    ('stage_encoded', 'Int'),
                    ('num_interactions', 'Int'),
                    ('days_since_last_contact', 'Int'),
                    ('num_open_deals', 'Int'),
                    ('avg_deal_value', 'Decimal'),
                    ('source_updated_at', 'Date'),
                    ('calculated_at', 'Date'),
                    ('contract_number', 'String'),
                    ('start_date', 'Date'),
                    ('end_date', 'Date'),
                    ('amount', 'Decimal'),
                    ('currency', 'String'),
                    ('signed_at', 'Date'),
                    ('contract_status', 'String')
                ) AS v(p_name, p_type)
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM property p
                    WHERE p.name = v.p_name
                      AND p.organization_id IS NULL
                );

                -- deal fields used by ML derivation
                INSERT INTO entity_type_property (entity_type_id, property_id, is_required)
                SELECT v_deal_type_id, p.id, req.is_required
                FROM property p
                JOIN (VALUES
                    ('created_at', true),
                    ('status', true)
                ) AS req(name, is_required) ON req.name = p.name
                WHERE p.organization_id IS NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM entity_type_property etp
                      WHERE etp.entity_type_id = v_deal_type_id
                        AND etp.property_id = p.id
                  );

                -- deal_analysis required ML features and freshness markers
                INSERT INTO entity_type_property (entity_type_id, property_id, is_required)
                SELECT v_deal_analysis_type_id, p.id, req.is_required
                FROM property p
                JOIN (VALUES
                    ('days_since_created', true),
                    ('stage_encoded', true),
                    ('num_interactions', true),
                    ('days_since_last_contact', true),
                    ('num_open_deals', true),
                    ('avg_deal_value', true),
                    ('source_updated_at', true),
                    ('calculated_at', true)
                ) AS req(name, is_required) ON req.name = p.name
                WHERE p.organization_id IS NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM entity_type_property etp
                      WHERE etp.entity_type_id = v_deal_analysis_type_id
                        AND etp.property_id = p.id
                  );

                -- contract required fields
                INSERT INTO entity_type_property (entity_type_id, property_id, is_required)
                SELECT v_contract_type_id, p.id, req.is_required
                FROM property p
                JOIN (VALUES
                    ('contract_number', true),
                    ('start_date', true),
                    ('end_date', true),
                    ('amount', true),
                    ('currency', true),
                    ('signed_at', true),
                    ('contract_status', true)
                ) AS req(name, is_required) ON req.name = p.name
                WHERE p.organization_id IS NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM entity_type_property etp
                      WHERE etp.entity_type_id = v_contract_type_id
                        AND etp.property_id = p.id
                  );

                INSERT INTO entity_relationship_type (name, source_entity_type_id, target_entity_type_id)
                SELECT 'deal_analysis', v_deal_type_id, v_deal_analysis_type_id
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM entity_relationship_type
                    WHERE name = 'deal_analysis'
                      AND source_entity_type_id = v_deal_type_id
                      AND target_entity_type_id = v_deal_analysis_type_id
                );

                INSERT INTO entity_relationship_type (name, source_entity_type_id, target_entity_type_id)
                SELECT 'deal_contract', v_deal_type_id, v_contract_type_id
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM entity_relationship_type
                    WHERE name = 'deal_contract'
                      AND source_entity_type_id = v_deal_type_id
                      AND target_entity_type_id = v_contract_type_id
                );
            END $$;
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                v_deal_analysis_type_id int;
                v_contract_type_id int;
            BEGIN
                SELECT id INTO v_deal_analysis_type_id FROM entity_type WHERE name = 'deal_analysis' LIMIT 1;
                SELECT id INTO v_contract_type_id FROM entity_type WHERE name = 'contract' LIMIT 1;

                DELETE FROM entity_relationship_type WHERE name IN ('deal_analysis', 'deal_contract');

                IF v_deal_analysis_type_id IS NOT NULL THEN
                    DELETE FROM entity_type_property WHERE entity_type_id = v_deal_analysis_type_id;
                END IF;

                IF v_contract_type_id IS NOT NULL THEN
                    DELETE FROM entity_type_property WHERE entity_type_id = v_contract_type_id;
                END IF;

                DELETE FROM entity_type_property
                WHERE entity_type_id IN (SELECT id FROM entity_type WHERE name = 'deal')
                  AND property_id IN (
                      SELECT id FROM property
                      WHERE name IN ('created_at', 'status')
                        AND organization_id IS NULL
                  );

                DELETE FROM entity_type WHERE name IN ('deal_analysis', 'contract');

                DELETE FROM property
                WHERE organization_id IS NULL
                  AND name IN (
                      'created_at',
                      'status',
                      'days_since_created',
                      'stage_encoded',
                      'num_interactions',
                      'days_since_last_contact',
                      'num_open_deals',
                      'avg_deal_value',
                      'source_updated_at',
                      'calculated_at',
                      'contract_number',
                      'start_date',
                      'end_date',
                      'amount',
                      'currency',
                      'signed_at',
                      'contract_status'
                  );
            END $$;
            """
        );
    }
}
