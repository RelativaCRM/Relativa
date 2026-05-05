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
                v_prop_deal_created_at int;
                v_prop_deal_status int;
                v_prop_deal_value int;
                v_prop_analysis_days_since_created int;
                v_prop_analysis_stage_encoded int;
                v_prop_analysis_num_interactions int;
                v_prop_analysis_days_since_last_contact int;
                v_prop_analysis_num_open_deals int;
                v_prop_analysis_avg_deal_value int;
                v_prop_analysis_source_updated_at int;
                v_prop_analysis_calculated_at int;
                v_prop_contract_number int;
                v_prop_contract_start_date int;
                v_prop_contract_end_date int;
                v_prop_contract_amount int;
                v_prop_contract_currency int;
                v_prop_contract_signed_at int;
                v_prop_contract_status int;
                v_analysis_entity_id int;
                v_contract_entity_id int;
                v_deal_id int;
                v_workspace_id int;
                v_rel_deal_client_id int;
                v_client_entity_id int;
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

                -- Resolve relationship and property ids for deterministic seed data.
                SELECT id INTO v_rel_deal_analysis_id FROM entity_relationship_type WHERE name = 'deal_analysis' AND source_entity_type_id = v_deal_type_id AND target_entity_type_id = v_deal_analysis_type_id LIMIT 1;
                SELECT id INTO v_rel_deal_contract_id FROM entity_relationship_type WHERE name = 'deal_contract' AND source_entity_type_id = v_deal_type_id AND target_entity_type_id = v_contract_type_id LIMIT 1;
                SELECT id INTO v_rel_deal_client_id FROM entity_relationship_type WHERE name = 'deal_client' AND source_entity_type_id = v_deal_type_id AND target_entity_type_id = v_client_type_id LIMIT 1;

                SELECT id INTO v_prop_deal_created_at FROM property WHERE name = 'created_at' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_deal_status FROM property WHERE name = 'status' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_deal_value FROM property WHERE name = 'deal_value' AND organization_id IS NULL LIMIT 1;

                SELECT id INTO v_prop_analysis_days_since_created FROM property WHERE name = 'days_since_created' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_analysis_stage_encoded FROM property WHERE name = 'stage_encoded' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_analysis_num_interactions FROM property WHERE name = 'num_interactions' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_analysis_days_since_last_contact FROM property WHERE name = 'days_since_last_contact' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_analysis_num_open_deals FROM property WHERE name = 'num_open_deals' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_analysis_avg_deal_value FROM property WHERE name = 'avg_deal_value' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_analysis_source_updated_at FROM property WHERE name = 'source_updated_at' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_analysis_calculated_at FROM property WHERE name = 'calculated_at' AND organization_id IS NULL LIMIT 1;

                SELECT id INTO v_prop_contract_number FROM property WHERE name = 'contract_number' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_contract_start_date FROM property WHERE name = 'start_date' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_contract_end_date FROM property WHERE name = 'end_date' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_contract_amount FROM property WHERE name = 'amount' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_contract_currency FROM property WHERE name = 'currency' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_contract_signed_at FROM property WHERE name = 'signed_at' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_contract_status FROM property WHERE name = 'contract_status' AND organization_id IS NULL LIMIT 1;

                -- On fresh DBs, create a small deterministic demo dataset (clients + deals) in first workspace.
                IF NOT EXISTS (
                    SELECT 1 FROM entity WHERE entity_type_id = v_deal_type_id AND is_archived = FALSE
                ) THEN
                    SELECT id INTO v_workspace_id FROM workspace WHERE is_archived = FALSE ORDER BY id LIMIT 1;

                    IF v_workspace_id IS NOT NULL THEN
                        FOR v_deal_id IN 1..3 LOOP
                            INSERT INTO entity (entity_type_id, is_archived)
                            VALUES (v_client_type_id, FALSE)
                            RETURNING id INTO v_client_entity_id;

                            INSERT INTO entity_workspace (entity_id, workspace_id)
                            VALUES (v_client_entity_id, v_workspace_id)
                            ON CONFLICT (entity_id, workspace_id) DO NOTHING;

                            INSERT INTO entity (entity_type_id, is_archived)
                            VALUES (v_deal_type_id, FALSE)
                            RETURNING id INTO v_deal_id;

                            INSERT INTO entity_workspace (entity_id, workspace_id)
                            VALUES (v_deal_id, v_workspace_id)
                            ON CONFLICT (entity_id, workspace_id) DO NOTHING;

                            INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                            VALUES (v_deal_id, v_prop_deal_value, NULL, NULL, 10000.00 + (v_deal_id * 2500.00), NULL, NULL)
                            ON CONFLICT (entity_id, property_id) DO UPDATE
                            SET value_decimal = EXCLUDED.value_decimal;

                            IF v_rel_deal_client_id IS NOT NULL THEN
                                INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                                VALUES (v_deal_id, v_client_entity_id, v_rel_deal_client_id);
                            END IF;
                        END LOOP;
                    END IF;
                END IF;

                -- Seed test-ready deal analysis for existing demo deals.
                FOR v_deal_id IN
                    SELECT id
                    FROM entity
                    WHERE entity_type_id = v_deal_type_id
                      AND is_archived = FALSE
                LOOP
                    -- Ensure deal.created_at and deal.status exist so recompute path can work deterministically.
                    INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                    VALUES (v_deal_id, v_prop_deal_created_at, NULL, NULL, NULL, NULL, DATE '2026-01-01')
                    ON CONFLICT (entity_id, property_id) DO NOTHING;

                    INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                    VALUES (v_deal_id, v_prop_deal_status, 'opened', NULL, NULL, NULL, NULL)
                    ON CONFLICT (entity_id, property_id) DO NOTHING;

                    -- Ensure deal_analysis entity relationship exists.
                    SELECT er.target_entity_id INTO v_analysis_entity_id
                    FROM entity_relationship er
                    WHERE er.relationship_type_id = v_rel_deal_analysis_id
                      AND er.source_entity_id = v_deal_id
                    LIMIT 1;

                    IF v_analysis_entity_id IS NULL THEN
                        INSERT INTO entity (entity_type_id, is_archived)
                        VALUES (v_deal_analysis_type_id, FALSE)
                        RETURNING id INTO v_analysis_entity_id;

                        INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                        VALUES (v_deal_id, v_analysis_entity_id, v_rel_deal_analysis_id);
                    END IF;

                    -- Seed complete feature vector and freshness values.
                    INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                    VALUES
                        (v_analysis_entity_id, v_prop_analysis_days_since_created, NULL, 45, NULL, NULL, NULL),
                        (v_analysis_entity_id, v_prop_analysis_stage_encoded, NULL, 2, NULL, NULL, NULL),
                        (v_analysis_entity_id, v_prop_analysis_num_interactions, NULL, 7, NULL, NULL, NULL),
                        (v_analysis_entity_id, v_prop_analysis_days_since_last_contact, NULL, 10, NULL, NULL, NULL),
                        (v_analysis_entity_id, v_prop_analysis_num_open_deals, NULL, 1, NULL, NULL, NULL),
                        (v_analysis_entity_id, v_prop_analysis_avg_deal_value, NULL, NULL, COALESCE((SELECT value_decimal FROM entity_property_value WHERE entity_id = v_deal_id AND property_id = v_prop_deal_value), 20000.00), NULL, NULL),
                        (v_analysis_entity_id, v_prop_analysis_source_updated_at, NULL, NULL, NULL, NULL, DATE '2026-05-05'),
                        (v_analysis_entity_id, v_prop_analysis_calculated_at, NULL, NULL, NULL, NULL, DATE '2026-05-05')
                    ON CONFLICT (entity_id, property_id) DO UPDATE SET
                        value_string = EXCLUDED.value_string,
                        value_int = EXCLUDED.value_int,
                        value_decimal = EXCLUDED.value_decimal,
                        value_bool = EXCLUDED.value_bool,
                        value_date = EXCLUDED.value_date;

                    -- Ensure at least one linked contract exists for each deal.
                    SELECT er.target_entity_id INTO v_contract_entity_id
                    FROM entity_relationship er
                    WHERE er.relationship_type_id = v_rel_deal_contract_id
                      AND er.source_entity_id = v_deal_id
                    ORDER BY er.target_entity_id
                    LIMIT 1;

                    IF v_contract_entity_id IS NULL THEN
                        INSERT INTO entity (entity_type_id, is_archived)
                        VALUES (v_contract_type_id, FALSE)
                        RETURNING id INTO v_contract_entity_id;

                        INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                        VALUES (v_deal_id, v_contract_entity_id, v_rel_deal_contract_id);
                    END IF;

                    INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                    VALUES
                        (v_contract_entity_id, v_prop_contract_number, 'CN-' || v_deal_id::text, NULL, NULL, NULL, NULL),
                        (v_contract_entity_id, v_prop_contract_start_date, NULL, NULL, NULL, NULL, DATE '2026-01-10'),
                        (v_contract_entity_id, v_prop_contract_end_date, NULL, NULL, NULL, NULL, DATE '2026-12-31'),
                        (v_contract_entity_id, v_prop_contract_amount, NULL, NULL, COALESCE((SELECT value_decimal FROM entity_property_value WHERE entity_id = v_deal_id AND property_id = v_prop_deal_value), 20000.00), NULL, NULL),
                        (v_contract_entity_id, v_prop_contract_currency, 'USD', NULL, NULL, NULL, NULL),
                        (v_contract_entity_id, v_prop_contract_signed_at, NULL, NULL, NULL, NULL, DATE '2026-02-01'),
                        (v_contract_entity_id, v_prop_contract_status, 'active', NULL, NULL, NULL, NULL)
                    ON CONFLICT (entity_id, property_id) DO UPDATE SET
                        value_string = EXCLUDED.value_string,
                        value_int = EXCLUDED.value_int,
                        value_decimal = EXCLUDED.value_decimal,
                        value_bool = EXCLUDED.value_bool,
                        value_date = EXCLUDED.value_date;

                    v_analysis_entity_id := NULL;
                    v_contract_entity_id := NULL;
                END LOOP;
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

                DELETE FROM entity_relationship
                WHERE relationship_type_id IN (
                    SELECT id FROM entity_relationship_type WHERE name IN ('deal_analysis', 'deal_contract')
                );

                DELETE FROM entity_property_value
                WHERE entity_id IN (
                    SELECT id FROM entity WHERE entity_type_id IN (v_deal_analysis_type_id, v_contract_type_id)
                );

                DELETE FROM entity
                WHERE entity_type_id IN (v_deal_analysis_type_id, v_contract_type_id);

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
