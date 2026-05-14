using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

/// <summary>
/// Seeds 6 clients and 10 deals with diverse ML-scoring profiles so the graph
/// highlight feature can distinguish best/worst entities. Only runs when fewer
/// than 5 non-archived deal entities exist (idempotent guard).
/// </summary>
[DbContext(typeof(MigrationDbContext))]
[Migration("20260509120000_RichMLHighlightSeedData")]
public partial class RichMLHighlightSeedData : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                -- entity type ids
                v_deal_type_id          int;
                v_client_type_id        int;
                v_deal_analysis_type_id int;
                v_contract_type_id      int;

                -- relationship type ids
                v_rel_deal_client_id    int;
                v_rel_deal_analysis_id  int;
                v_rel_deal_contract_id  int;

                -- client property ids
                v_prop_first_name       int;
                v_prop_last_name        int;

                -- deal property ids
                v_prop_deal_value       int;
                v_prop_expected_close   int;
                v_prop_created_at       int;
                v_prop_status           int;

                -- deal_analysis property ids
                v_prop_days_since_created     int;
                v_prop_stage_encoded          int;
                v_prop_num_interactions       int;
                v_prop_days_since_last        int;
                v_prop_num_open_deals         int;
                v_prop_avg_deal_value         int;
                v_prop_source_updated_at      int;
                v_prop_calculated_at          int;
                v_prop_days_until_close       int;
                v_prop_hist_close_rate        int;

                -- contract property ids
                v_prop_contract_number  int;
                v_prop_start_date       int;
                v_prop_end_date         int;
                v_prop_amount           int;
                v_prop_currency         int;
                v_prop_signed_at        int;
                v_prop_contract_status  int;
                v_prop_contract_type    int;

                -- scratch
                v_workspace_id   int;
                v_client_ids     int[] := ARRAY[]::int[];
                v_deal_ids       int[]  := ARRAY[]::int[];
                v_new_entity_id  int;
                v_analysis_id    int;
                v_contract_id    int;
                i                int;
            BEGIN
                -- ── guard ───────────────────────────────────────────────────────
                SELECT id INTO v_deal_type_id   FROM entity_type WHERE name = 'deal'          LIMIT 1;
                SELECT id INTO v_client_type_id FROM entity_type WHERE name = 'client'        LIMIT 1;

                IF v_deal_type_id IS NULL OR v_client_type_id IS NULL THEN RETURN; END IF;

                IF (SELECT COUNT(*) FROM entity WHERE entity_type_id = v_deal_type_id AND is_archived = FALSE) >= 5 THEN
                    RETURN;
                END IF;

                SELECT id INTO v_workspace_id FROM workspace WHERE is_archived = FALSE ORDER BY id LIMIT 1;
                IF v_workspace_id IS NULL THEN RETURN; END IF;

                -- ── resolve types / relationships ────────────────────────────────
                SELECT id INTO v_deal_analysis_type_id FROM entity_type WHERE name = 'deal_analysis' LIMIT 1;
                SELECT id INTO v_contract_type_id      FROM entity_type WHERE name = 'contract'      LIMIT 1;

                SELECT id INTO v_rel_deal_client_id   FROM entity_relationship_type WHERE name = 'deal_client'   LIMIT 1;
                SELECT id INTO v_rel_deal_analysis_id FROM entity_relationship_type WHERE name = 'deal_analysis' LIMIT 1;
                SELECT id INTO v_rel_deal_contract_id FROM entity_relationship_type WHERE name = 'deal_contract' LIMIT 1;

                -- ── resolve property ids ─────────────────────────────────────────
                SELECT id INTO v_prop_first_name   FROM property WHERE name = 'first_name'   AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_last_name    FROM property WHERE name = 'last_name'    AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_deal_value   FROM property WHERE name = 'deal_value'   AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_expected_close FROM property WHERE name = 'expected_close' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_created_at   FROM property WHERE name = 'created_at'   AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_status       FROM property WHERE name = 'status'       AND organization_id IS NULL LIMIT 1;

                SELECT id INTO v_prop_days_since_created  FROM property WHERE name = 'days_since_created'      AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_stage_encoded       FROM property WHERE name = 'stage_encoded'           AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_num_interactions    FROM property WHERE name = 'num_interactions'        AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_days_since_last     FROM property WHERE name = 'days_since_last_contact' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_num_open_deals      FROM property WHERE name = 'num_open_deals'          AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_avg_deal_value      FROM property WHERE name = 'avg_deal_value'          AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_source_updated_at   FROM property WHERE name = 'source_updated_at'       AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_calculated_at       FROM property WHERE name = 'calculated_at'           AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_days_until_close    FROM property WHERE name = 'days_until_expected_close' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_hist_close_rate     FROM property WHERE name = 'historical_close_rate'   AND organization_id IS NULL LIMIT 1;

                SELECT id INTO v_prop_contract_number FROM property WHERE name = 'contract_number' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_start_date      FROM property WHERE name = 'start_date'      AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_end_date        FROM property WHERE name = 'end_date'        AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_amount          FROM property WHERE name = 'amount'          AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_currency        FROM property WHERE name = 'currency'        AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_signed_at       FROM property WHERE name = 'signed_at'       AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_contract_status FROM property WHERE name = 'contract_status' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_contract_type   FROM property WHERE name = 'contract_type'   AND organization_id IS NULL LIMIT 1;

                -- ── create 6 client entities ─────────────────────────────────────
                -- first_name, last_name pairs: Alpha Corp, Beta Ltd, Gamma Inc, Delta Co, Epsilon Group, Zeta Partners
                FOR i IN 1..6 LOOP
                    INSERT INTO entity (entity_type_id, is_archived) VALUES (v_client_type_id, FALSE)
                    RETURNING id INTO v_new_entity_id;

                    INSERT INTO entity_workspace (entity_id, workspace_id) VALUES (v_new_entity_id, v_workspace_id)
                    ON CONFLICT DO NOTHING;

                    INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                    SELECT v_new_entity_id, prop, val, NULL, NULL, NULL, NULL
                    FROM (
                        SELECT v_prop_first_name AS prop,
                               CASE i WHEN 1 THEN 'Alpha' WHEN 2 THEN 'Beta' WHEN 3 THEN 'Gamma'
                                      WHEN 4 THEN 'Delta' WHEN 5 THEN 'Epsilon' ELSE 'Zeta' END AS val
                        UNION ALL
                        SELECT v_prop_last_name,
                               CASE i WHEN 1 THEN 'Corp' WHEN 2 THEN 'Ltd' WHEN 3 THEN 'Inc'
                                      WHEN 4 THEN 'Co' WHEN 5 THEN 'Group' ELSE 'Partners' END
                    ) AS vals
                    WHERE prop IS NOT NULL;

                    v_client_ids := v_client_ids || v_new_entity_id;
                END LOOP;

                -- ── create 10 deal entities ───────────────────────────────────────
                -- profiles: (deal_value, status, expected_close, created_at date, client_idx 1-based)
                -- Best: D1(95000,closed,2026-06-01,c1), D2(82000,closed,2026-06-15,c1),
                --        D5(88000,opened,2026-05-30,c5), D10(110000,closed,2026-05-25,c5)
                -- Worst: D6(12000,revoked,2025-12-01,c3), D7(8000,revoked,2025-11-01,c4),
                --         D9(15000,revoked,2026-01-01,c3)
                -- Mid:  D3(67000,pending,2026-07-01,c2), D4(58000,pending,2026-07-20,c2),
                --        D8(45000,opened,2026-08-01,c6)
                FOR i IN 1..10 LOOP
                    INSERT INTO entity (entity_type_id, is_archived) VALUES (v_deal_type_id, FALSE)
                    RETURNING id INTO v_new_entity_id;

                    INSERT INTO entity_workspace (entity_id, workspace_id) VALUES (v_new_entity_id, v_workspace_id)
                    ON CONFLICT DO NOTHING;

                    -- seed deal_value, status, expected_close, created_at
                    INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                    SELECT v_new_entity_id, prop, val_s, NULL, val_d, NULL, val_date
                    FROM (
                        SELECT v_prop_deal_value AS prop, NULL::text AS val_s,
                            CASE i WHEN 1 THEN 95000 WHEN 2 THEN 82000 WHEN 3 THEN 67000 WHEN 4 THEN 58000
                                   WHEN 5 THEN 88000 WHEN 6 THEN 12000 WHEN 7 THEN 8000  WHEN 8 THEN 45000
                                   WHEN 9 THEN 15000 ELSE 110000 END::decimal AS val_d,
                            NULL::date AS val_date
                        UNION ALL
                        SELECT v_prop_status, CASE i WHEN 1 THEN 'closed' WHEN 2 THEN 'closed'
                            WHEN 3 THEN 'pending' WHEN 4 THEN 'pending' WHEN 5 THEN 'opened'
                            WHEN 6 THEN 'revoked' WHEN 7 THEN 'revoked' WHEN 8 THEN 'opened'
                            WHEN 9 THEN 'revoked' ELSE 'closed' END, NULL, NULL
                        UNION ALL
                        SELECT v_prop_expected_close, NULL, NULL,
                            CASE i WHEN 1 THEN DATE '2026-06-01' WHEN 2 THEN DATE '2026-06-15'
                                   WHEN 3 THEN DATE '2026-07-01' WHEN 4 THEN DATE '2026-07-20'
                                   WHEN 5 THEN DATE '2026-05-30' WHEN 6 THEN DATE '2025-12-01'
                                   WHEN 7 THEN DATE '2025-11-01' WHEN 8 THEN DATE '2026-08-01'
                                   WHEN 9 THEN DATE '2026-01-01' ELSE DATE '2026-05-25' END
                        UNION ALL
                        SELECT v_prop_created_at, NULL, NULL,
                            CASE i WHEN 1 THEN DATE '2026-04-25' WHEN 2 THEN DATE '2026-04-10'
                                   WHEN 3 THEN DATE '2026-04-03' WHEN 4 THEN DATE '2026-03-21'
                                   WHEN 5 THEN DATE '2026-04-30' WHEN 6 THEN DATE '2025-11-16'
                                   WHEN 7 THEN DATE '2025-10-27' WHEN 8 THEN DATE '2026-03-16'
                                   WHEN 9 THEN DATE '2025-12-16' ELSE DATE '2026-04-20' END
                    ) AS vals(prop, val_s, val_d, val_date)
                    WHERE prop IS NOT NULL;

                    -- link deal → client
                    IF v_rel_deal_client_id IS NOT NULL THEN
                        INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                        VALUES (
                            v_new_entity_id,
                            v_client_ids[CASE i WHEN 1 THEN 1 WHEN 2 THEN 1 WHEN 3 THEN 2 WHEN 4 THEN 2
                                                WHEN 5 THEN 5 WHEN 6 THEN 3 WHEN 7 THEN 4 WHEN 8 THEN 6
                                                WHEN 9 THEN 3 ELSE 5 END],
                            v_rel_deal_client_id
                        );
                    END IF;

                    v_deal_ids := v_deal_ids || v_new_entity_id;
                END LOOP;

                -- ── create deal_analysis entities for each deal ───────────────────
                FOR i IN 1..10 LOOP
                    IF v_deal_analysis_type_id IS NULL OR v_rel_deal_analysis_id IS NULL THEN EXIT; END IF;

                    INSERT INTO entity (entity_type_id, is_archived) VALUES (v_deal_analysis_type_id, FALSE)
                    RETURNING id INTO v_analysis_id;

                    INSERT INTO entity_workspace (entity_id, workspace_id) VALUES (v_analysis_id, v_workspace_id)
                    ON CONFLICT DO NOTHING;

                    INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                    VALUES (v_deal_ids[i], v_analysis_id, v_rel_deal_analysis_id);

                    -- seed feature vector (days_until_expected_close + historical_close_rate stay NULL for ML)
                    INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                    SELECT v_analysis_id, prop, NULL, val_i, val_d, NULL, val_date
                    FROM (
                        SELECT v_prop_days_since_created AS prop,
                            CASE i WHEN 1 THEN 20 WHEN 2 THEN 35 WHEN 3 THEN 42 WHEN 4 THEN 55
                                   WHEN 5 THEN 15 WHEN 6 THEN 180 WHEN 7 THEN 200 WHEN 8 THEN 60
                                   WHEN 9 THEN 150 ELSE 25 END::int AS val_i,
                            NULL::decimal AS val_d, NULL::date AS val_date
                        UNION ALL
                        SELECT v_prop_stage_encoded,
                            CASE i WHEN 1 THEN 4 WHEN 2 THEN 4 WHEN 3 THEN 3 WHEN 4 THEN 3
                                   WHEN 5 THEN 4 WHEN 6 THEN 0 WHEN 7 THEN 0 WHEN 8 THEN 2
                                   WHEN 9 THEN 0 ELSE 4 END, NULL, NULL
                        UNION ALL
                        SELECT v_prop_num_interactions,
                            CASE i WHEN 1 THEN 18 WHEN 2 THEN 15 WHEN 3 THEN 11 WHEN 4 THEN 9
                                   WHEN 5 THEN 20 WHEN 6 THEN 2 WHEN 7 THEN 1 WHEN 8 THEN 6
                                   WHEN 9 THEN 3 ELSE 22 END, NULL, NULL
                        UNION ALL
                        SELECT v_prop_days_since_last,
                            CASE i WHEN 1 THEN 2 WHEN 2 THEN 4 WHEN 3 THEN 7 WHEN 4 THEN 10
                                   WHEN 5 THEN 1 WHEN 6 THEN 90 WHEN 7 THEN 120 WHEN 8 THEN 15
                                   WHEN 9 THEN 75 ELSE 1 END, NULL, NULL
                        UNION ALL
                        SELECT v_prop_num_open_deals,
                            CASE i WHEN 1 THEN 1 WHEN 2 THEN 1 WHEN 3 THEN 2 WHEN 4 THEN 2
                                   WHEN 5 THEN 1 WHEN 6 THEN 3 WHEN 7 THEN 4 WHEN 8 THEN 2
                                   WHEN 9 THEN 3 ELSE 1 END, NULL, NULL
                        UNION ALL
                        SELECT v_prop_avg_deal_value, NULL,
                            CASE i WHEN 1 THEN 95000 WHEN 2 THEN 82000 WHEN 3 THEN 67000 WHEN 4 THEN 58000
                                   WHEN 5 THEN 88000 WHEN 6 THEN 12000 WHEN 7 THEN 8000 WHEN 8 THEN 45000
                                   WHEN 9 THEN 15000 ELSE 110000 END::decimal, NULL
                        UNION ALL
                        SELECT v_prop_source_updated_at, NULL, NULL, DATE '2026-05-09'
                        UNION ALL
                        SELECT v_prop_calculated_at, NULL, NULL, DATE '2026-05-09'
                    ) AS vals(prop, val_i, val_d, val_date)
                    WHERE prop IS NOT NULL;
                END LOOP;

                -- ── create contracts for each deal ────────────────────────────────
                FOR i IN 1..10 LOOP
                    IF v_contract_type_id IS NULL OR v_rel_deal_contract_id IS NULL THEN EXIT; END IF;

                    INSERT INTO entity (entity_type_id, is_archived) VALUES (v_contract_type_id, FALSE)
                    RETURNING id INTO v_contract_id;

                    INSERT INTO entity_workspace (entity_id, workspace_id) VALUES (v_contract_id, v_workspace_id)
                    ON CONFLICT DO NOTHING;

                    INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                    VALUES (v_deal_ids[i], v_contract_id, v_rel_deal_contract_id);

                    INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                    SELECT v_contract_id, prop, val_s, NULL, val_d, NULL, val_date
                    FROM (
                        SELECT v_prop_contract_number AS prop,
                            'CN-SEED-' || i::text AS val_s, NULL::decimal AS val_d, NULL::date AS val_date
                        UNION ALL
                        SELECT v_prop_start_date, NULL, NULL,
                            CASE i WHEN 1 THEN DATE '2026-01-15' WHEN 2 THEN DATE '2026-02-01'
                                   WHEN 3 THEN DATE '2026-04-01' WHEN 4 THEN DATE '2026-04-15'
                                   WHEN 5 THEN DATE '2026-05-01' WHEN 6 THEN DATE '2025-06-01'
                                   WHEN 7 THEN DATE '2025-05-01' WHEN 8 THEN DATE '2026-03-01'
                                   WHEN 9 THEN DATE '2025-10-01' ELSE DATE '2026-02-15' END
                        UNION ALL
                        SELECT v_prop_end_date, NULL, NULL,
                            CASE i WHEN 1 THEN DATE '2026-12-31' WHEN 2 THEN DATE '2026-12-31'
                                   WHEN 3 THEN DATE '2026-12-31' WHEN 4 THEN DATE '2026-12-31'
                                   WHEN 5 THEN DATE '2026-12-31' WHEN 6 THEN DATE '2025-12-01'
                                   WHEN 7 THEN DATE '2025-11-01' WHEN 8 THEN DATE '2027-02-28'
                                   WHEN 9 THEN DATE '2026-01-01' ELSE DATE '2026-12-31' END
                        UNION ALL
                        SELECT v_prop_amount, NULL,
                            CASE i WHEN 1 THEN 95000 WHEN 2 THEN 82000 WHEN 3 THEN 67000 WHEN 4 THEN 58000
                                   WHEN 5 THEN 88000 WHEN 6 THEN 12000 WHEN 7 THEN 8000 WHEN 8 THEN 45000
                                   WHEN 9 THEN 15000 ELSE 110000 END::decimal, NULL
                        UNION ALL
                        SELECT v_prop_currency,
                            CASE i WHEN 3 THEN 'EUR' WHEN 4 THEN 'EUR' WHEN 8 THEN 'GBP' ELSE 'USD' END,
                            NULL, NULL
                        UNION ALL
                        SELECT v_prop_signed_at, NULL, NULL,
                            CASE i WHEN 1 THEN DATE '2026-01-20' WHEN 2 THEN DATE '2026-02-05'
                                   WHEN 3 THEN DATE '2026-04-10' WHEN 4 THEN DATE '2026-04-20'
                                   WHEN 5 THEN DATE '2026-05-05' WHEN 6 THEN DATE '2025-06-15'
                                   WHEN 7 THEN DATE '2025-05-20' WHEN 8 THEN DATE '2026-03-10'
                                   WHEN 9 THEN DATE '2025-10-15' ELSE DATE '2026-02-20' END
                        UNION ALL
                        SELECT v_prop_contract_status,
                            CASE WHEN i IN (6, 7, 9) THEN 'revoked' ELSE 'active' END, NULL, NULL
                        UNION ALL
                        SELECT v_prop_contract_type,
                            CASE i WHEN 1 THEN 'subscription' WHEN 2 THEN 'one_time'
                                   WHEN 3 THEN 'retainer'     WHEN 4 THEN 'subscription'
                                   WHEN 5 THEN 'one_time'     WHEN 6 THEN 'one_time'
                                   WHEN 7 THEN 'one_time'     WHEN 8 THEN 'retainer'
                                   WHEN 9 THEN 'one_time'     ELSE 'subscription' END, NULL, NULL
                    ) AS vals(prop, val_s, val_d, val_date)
                    WHERE prop IS NOT NULL;
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
                v_deal_type_id   int;
                v_client_type_id int;
            BEGIN
                SELECT id INTO v_deal_type_id   FROM entity_type WHERE name = 'deal'   LIMIT 1;
                SELECT id INTO v_client_type_id FROM entity_type WHERE name = 'client' LIMIT 1;

                DELETE FROM entity_relationship
                WHERE source_entity_id IN (
                    SELECT id FROM entity WHERE entity_type_id = v_deal_type_id AND is_archived = FALSE
                );

                DELETE FROM entity WHERE entity_type_id IN (
                    SELECT id FROM entity_type WHERE name IN ('deal', 'client', 'deal_analysis', 'contract')
                ) AND is_archived = FALSE;
            END $$;
            """
        );
    }
}
