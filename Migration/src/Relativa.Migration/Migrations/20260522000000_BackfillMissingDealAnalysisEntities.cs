using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

/// <summary>
/// Backfills deal_analysis entities for any active deal that currently lacks one.
/// Safe to run on any environment — idempotent, targets only the gap.
/// Affected by: AddApacWorkspaceAndSeedData (20260521110000) seeded deals without deal_analysis.
/// The calculated_at is seeded one day in the past so the ML scoring path immediately
/// recomputes a fresh feature vector on the next score request.
/// </summary>
[DbContext(typeof(MigrationDbContext))]
[Migration("20260522000000_BackfillMissingDealAnalysisEntities")]
public partial class BackfillMissingDealAnalysisEntities : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                v_deal_type_id          int;
                v_deal_analysis_type_id int;
                v_rel_deal_analysis_id  int;
                v_prop_deal_value       int;
                v_prop_days_since_created       int;
                v_prop_stage_encoded            int;
                v_prop_num_interactions         int;
                v_prop_days_since_last_contact  int;
                v_prop_num_open_deals           int;
                v_prop_avg_deal_value           int;
                v_prop_source_updated_at        int;
                v_prop_calculated_at            int;
                v_deal_id               int;
                v_analysis_entity_id    int;
            BEGIN
                SELECT id INTO v_deal_type_id          FROM entity_type WHERE name = 'deal'          LIMIT 1;
                SELECT id INTO v_deal_analysis_type_id FROM entity_type WHERE name = 'deal_analysis' LIMIT 1;
                SELECT id INTO v_rel_deal_analysis_id  FROM entity_relationship_type WHERE name = 'deal_analysis' LIMIT 1;

                -- If the deal_analysis schema is not set up yet, nothing to do.
                IF v_deal_type_id IS NULL OR v_deal_analysis_type_id IS NULL OR v_rel_deal_analysis_id IS NULL THEN
                    RAISE NOTICE 'deal_analysis schema not found — skipping backfill';
                    RETURN;
                END IF;

                SELECT id INTO v_prop_deal_value              FROM property WHERE name = 'deal_value'             AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_days_since_created      FROM property WHERE name = 'days_since_created'     AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_stage_encoded           FROM property WHERE name = 'stage_encoded'          AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_num_interactions        FROM property WHERE name = 'num_interactions'       AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_days_since_last_contact FROM property WHERE name = 'days_since_last_contact' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_num_open_deals          FROM property WHERE name = 'num_open_deals'         AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_avg_deal_value          FROM property WHERE name = 'avg_deal_value'         AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_source_updated_at       FROM property WHERE name = 'source_updated_at'      AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_calculated_at           FROM property WHERE name = 'calculated_at'          AND organization_id IS NULL LIMIT 1;

                -- Only proceed if all required property IDs resolved.
                IF v_prop_days_since_created IS NULL OR v_prop_stage_encoded IS NULL
                    OR v_prop_num_interactions IS NULL OR v_prop_days_since_last_contact IS NULL
                    OR v_prop_num_open_deals IS NULL OR v_prop_avg_deal_value IS NULL
                    OR v_prop_source_updated_at IS NULL OR v_prop_calculated_at IS NULL THEN
                    RAISE NOTICE 'One or more analysis property IDs not found — skipping backfill';
                    RETURN;
                END IF;

                FOR v_deal_id IN
                    SELECT e.id
                    FROM entity e
                    WHERE e.entity_type_id = v_deal_type_id
                      AND e.is_archived = FALSE
                      AND NOT EXISTS (
                          SELECT 1 FROM entity_relationship er
                          WHERE er.relationship_type_id = v_rel_deal_analysis_id
                            AND er.source_entity_id = e.id
                      )
                LOOP
                    INSERT INTO entity (entity_type_id, created_by_user_id, is_archived)
                    SELECT v_deal_analysis_type_id, e.created_by_user_id, FALSE
                    FROM entity e WHERE e.id = v_deal_id
                    RETURNING id INTO v_analysis_entity_id;

                    INSERT INTO entity_workspace (entity_id, workspace_id)
                    SELECT v_analysis_entity_id, ew.workspace_id
                    FROM entity_workspace ew
                    WHERE ew.entity_id = v_deal_id
                    ON CONFLICT (entity_id, workspace_id) DO NOTHING;

                    INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                    VALUES (v_deal_id, v_analysis_entity_id, v_rel_deal_analysis_id);

                    -- Seed a placeholder feature vector. calculated_at is one day in the past so the ML
                    -- recompute path immediately overwrites these values on the next score request.
                    INSERT INTO entity_property_value (entity_id, property_id, value_int, value_decimal, value_date)
                    VALUES
                        (v_analysis_entity_id, v_prop_days_since_created,      30,    NULL, NULL),
                        (v_analysis_entity_id, v_prop_stage_encoded,            1,    NULL, NULL),
                        (v_analysis_entity_id, v_prop_num_interactions,         4,    NULL, NULL),
                        (v_analysis_entity_id, v_prop_days_since_last_contact, 15,    NULL, NULL),
                        (v_analysis_entity_id, v_prop_num_open_deals,           0,    NULL, NULL),
                        (v_analysis_entity_id, v_prop_avg_deal_value,        NULL,
                            COALESCE(
                                (SELECT epv.value_decimal FROM entity_property_value epv
                                 WHERE epv.entity_id = v_deal_id AND epv.property_id = v_prop_deal_value),
                                10000.00
                            ),
                            NULL),
                        (v_analysis_entity_id, v_prop_source_updated_at,     NULL,  NULL, CURRENT_DATE),
                        (v_analysis_entity_id, v_prop_calculated_at,         NULL,  NULL, CURRENT_DATE - 1)
                    ON CONFLICT (entity_id, property_id) DO NOTHING;

                    v_analysis_entity_id := NULL;
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
