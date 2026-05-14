using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

[DbContext(typeof(MigrationDbContext))]
[Migration("20260505145000_BackfillDealAnalysisSeedData")]
public partial class BackfillDealAnalysisSeedData : EfMigration
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
                v_rel_deal_client_id int;
                v_workspace_id int;
                v_analysis_entity_id int;
                v_contract_entity_id int;
                v_client_entity_id int;
                v_deal_id int;
                v_prop_deal_value int;
                v_prop_deal_created_at int;
                v_prop_deal_status int;
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
            BEGIN
                SELECT id INTO v_deal_type_id FROM entity_type WHERE name = 'deal' LIMIT 1;
                SELECT id INTO v_client_type_id FROM entity_type WHERE name = 'client' LIMIT 1;
                SELECT id INTO v_deal_analysis_type_id FROM entity_type WHERE name = 'deal_analysis' LIMIT 1;
                SELECT id INTO v_contract_type_id FROM entity_type WHERE name = 'contract' LIMIT 1;

                SELECT id INTO v_rel_deal_analysis_id FROM entity_relationship_type WHERE name = 'deal_analysis' LIMIT 1;
                SELECT id INTO v_rel_deal_contract_id FROM entity_relationship_type WHERE name = 'deal_contract' LIMIT 1;
                SELECT id INTO v_rel_deal_client_id FROM entity_relationship_type WHERE name = 'deal_client' LIMIT 1;

                SELECT id INTO v_prop_deal_value FROM property WHERE name = 'deal_value' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_deal_created_at FROM property WHERE name = 'created_at' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_deal_status FROM property WHERE name = 'status' AND organization_id IS NULL LIMIT 1;
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

                -- If DB has no active deals, create a small baseline sample.
                IF NOT EXISTS (SELECT 1 FROM entity WHERE entity_type_id = v_deal_type_id AND is_archived = FALSE) THEN
                    SELECT id INTO v_workspace_id FROM workspace WHERE is_archived = FALSE ORDER BY id LIMIT 1;
                    IF v_workspace_id IS NOT NULL THEN
                        FOR v_deal_id IN 1..3 LOOP
                            INSERT INTO entity (entity_type_id, is_archived) VALUES (v_client_type_id, FALSE) RETURNING id INTO v_client_entity_id;
                            INSERT INTO entity_workspace (entity_id, workspace_id) VALUES (v_client_entity_id, v_workspace_id) ON CONFLICT (entity_id, workspace_id) DO NOTHING;

                            INSERT INTO entity (entity_type_id, is_archived) VALUES (v_deal_type_id, FALSE) RETURNING id INTO v_deal_id;
                            INSERT INTO entity_workspace (entity_id, workspace_id) VALUES (v_deal_id, v_workspace_id) ON CONFLICT (entity_id, workspace_id) DO NOTHING;

                            INSERT INTO entity_property_value (entity_id, property_id, value_decimal)
                            VALUES (v_deal_id, v_prop_deal_value, 15000.00 + (v_deal_id * 1200.00))
                            ON CONFLICT (entity_id, property_id) DO UPDATE SET value_decimal = EXCLUDED.value_decimal;

                            IF v_rel_deal_client_id IS NOT NULL THEN
                                INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                                VALUES (v_deal_id, v_client_entity_id, v_rel_deal_client_id);
                            END IF;
                        END LOOP;
                    END IF;
                END IF;

                -- Backfill all active deals with complete analysis + contract rows.
                FOR v_deal_id IN
                    SELECT id FROM entity WHERE entity_type_id = v_deal_type_id AND is_archived = FALSE
                LOOP
                    INSERT INTO entity_property_value (entity_id, property_id, value_date)
                    VALUES (v_deal_id, v_prop_deal_created_at, DATE '2026-01-01')
                    ON CONFLICT (entity_id, property_id) DO NOTHING;

                    INSERT INTO entity_property_value (entity_id, property_id, value_string)
                    VALUES (v_deal_id, v_prop_deal_status, 'opened')
                    ON CONFLICT (entity_id, property_id) DO NOTHING;

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

                    INSERT INTO entity_workspace (entity_id, workspace_id)
                    SELECT v_analysis_entity_id, ew.workspace_id
                    FROM entity_workspace ew
                    WHERE ew.entity_id = v_deal_id
                    ON CONFLICT (entity_id, workspace_id) DO NOTHING;

                    INSERT INTO entity_property_value (entity_id, property_id, value_int, value_decimal, value_date)
                    VALUES
                        (v_analysis_entity_id, v_prop_analysis_days_since_created, 45, NULL, NULL),
                        (v_analysis_entity_id, v_prop_analysis_stage_encoded, 2, NULL, NULL),
                        (v_analysis_entity_id, v_prop_analysis_num_interactions, 7, NULL, NULL),
                        (v_analysis_entity_id, v_prop_analysis_days_since_last_contact, 10, NULL, NULL),
                        (v_analysis_entity_id, v_prop_analysis_num_open_deals, 1, NULL, NULL),
                        (v_analysis_entity_id, v_prop_analysis_avg_deal_value, NULL, COALESCE((SELECT value_decimal FROM entity_property_value WHERE entity_id = v_deal_id AND property_id = v_prop_deal_value), 20000.00), NULL),
                        (v_analysis_entity_id, v_prop_analysis_source_updated_at, NULL, NULL, DATE '2026-05-05'),
                        (v_analysis_entity_id, v_prop_analysis_calculated_at, NULL, NULL, DATE '2026-05-05')
                    ON CONFLICT (entity_id, property_id) DO UPDATE SET
                        value_int = EXCLUDED.value_int,
                        value_decimal = EXCLUDED.value_decimal,
                        value_date = EXCLUDED.value_date;

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

                    INSERT INTO entity_workspace (entity_id, workspace_id)
                    SELECT v_contract_entity_id, ew.workspace_id
                    FROM entity_workspace ew
                    WHERE ew.entity_id = v_deal_id
                    ON CONFLICT (entity_id, workspace_id) DO NOTHING;

                    INSERT INTO entity_property_value (entity_id, property_id, value_string, value_decimal, value_date)
                    VALUES
                        (v_contract_entity_id, v_prop_contract_number, 'CN-' || v_deal_id::text, NULL, NULL),
                        (v_contract_entity_id, v_prop_contract_currency, 'USD', NULL, NULL),
                        (v_contract_entity_id, v_prop_contract_status, 'active', NULL, NULL),
                        (v_contract_entity_id, v_prop_contract_amount, NULL, COALESCE((SELECT value_decimal FROM entity_property_value WHERE entity_id = v_deal_id AND property_id = v_prop_deal_value), 20000.00), NULL),
                        (v_contract_entity_id, v_prop_contract_start_date, NULL, NULL, DATE '2026-01-10'),
                        (v_contract_entity_id, v_prop_contract_end_date, NULL, NULL, DATE '2026-12-31'),
                        (v_contract_entity_id, v_prop_contract_signed_at, NULL, NULL, DATE '2026-02-01')
                    ON CONFLICT (entity_id, property_id) DO UPDATE SET
                        value_string = EXCLUDED.value_string,
                        value_decimal = EXCLUDED.value_decimal,
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
        // Backfill-only migration: no destructive rollback.
    }
}
