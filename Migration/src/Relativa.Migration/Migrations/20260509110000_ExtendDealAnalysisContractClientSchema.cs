using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

[DbContext(typeof(MigrationDbContext))]
[Migration("20260509110000_ExtendDealAnalysisContractClientSchema")]
public partial class ExtendDealAnalysisContractClientSchema : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                v_deal_type_id          int;
                v_deal_analysis_type_id int;
                v_contract_type_id      int;
                v_client_type_id        int;
                v_prop_expected_close   int;
                v_prop_days_until_close int;
                v_prop_hist_close_rate  int;
                v_prop_contract_type    int;
                v_prop_client_ltv       int;
                v_prop_client_tenure    int;
            BEGIN
                SELECT id INTO v_deal_type_id          FROM entity_type WHERE name = 'deal'          LIMIT 1;
                SELECT id INTO v_deal_analysis_type_id FROM entity_type WHERE name = 'deal_analysis' LIMIT 1;
                SELECT id INTO v_contract_type_id      FROM entity_type WHERE name = 'contract'      LIMIT 1;
                SELECT id INTO v_client_type_id        FROM entity_type WHERE name = 'client'        LIMIT 1;

                -- Insert new global properties
                INSERT INTO property (name, data_type, organization_id, is_readonly)
                SELECT p_name, p_type, NULL, p_ro
                FROM (VALUES
                    ('expected_close',            'Date',    false),
                    ('days_until_expected_close', 'Int',     true),
                    ('historical_close_rate',     'Decimal', true),
                    ('contract_type',             'String',  false),
                    ('client_lifetime_value',     'Decimal', true),
                    ('client_tenure_days',        'Int',     true)
                ) AS v(p_name, p_type, p_ro)
                WHERE NOT EXISTS (
                    SELECT 1 FROM property p WHERE p.name = v.p_name AND p.organization_id IS NULL
                );

                SELECT id INTO v_prop_expected_close   FROM property WHERE name = 'expected_close'            AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_days_until_close FROM property WHERE name = 'days_until_expected_close' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_hist_close_rate  FROM property WHERE name = 'historical_close_rate'     AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_contract_type    FROM property WHERE name = 'contract_type'             AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_client_ltv       FROM property WHERE name = 'client_lifetime_value'     AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_client_tenure    FROM property WHERE name = 'client_tenure_days'        AND organization_id IS NULL LIMIT 1;

                -- Bind deal.expected_close
                IF v_deal_type_id IS NOT NULL AND v_prop_expected_close IS NOT NULL THEN
                    INSERT INTO entity_type_property (entity_type_id, property_id, is_required)
                    SELECT v_deal_type_id, v_prop_expected_close, FALSE
                    WHERE NOT EXISTS (
                        SELECT 1 FROM entity_type_property
                        WHERE entity_type_id = v_deal_type_id AND property_id = v_prop_expected_close
                    );
                END IF;

                -- Bind deal_analysis.days_until_expected_close + historical_close_rate
                IF v_deal_analysis_type_id IS NOT NULL THEN
                    INSERT INTO entity_type_property (entity_type_id, property_id, is_required)
                    SELECT v_deal_analysis_type_id, t.p_id, FALSE
                    FROM (VALUES (v_prop_days_until_close), (v_prop_hist_close_rate)) AS t(p_id)
                    WHERE t.p_id IS NOT NULL
                      AND NOT EXISTS (
                          SELECT 1 FROM entity_type_property
                          WHERE entity_type_id = v_deal_analysis_type_id AND property_id = t.p_id
                      );
                END IF;

                -- Bind contract.contract_type
                IF v_contract_type_id IS NOT NULL AND v_prop_contract_type IS NOT NULL THEN
                    INSERT INTO entity_type_property (entity_type_id, property_id, is_required)
                    SELECT v_contract_type_id, v_prop_contract_type, FALSE
                    WHERE NOT EXISTS (
                        SELECT 1 FROM entity_type_property
                        WHERE entity_type_id = v_contract_type_id AND property_id = v_prop_contract_type
                    );
                END IF;

                -- Seed contract_type allowed values
                IF v_prop_contract_type IS NOT NULL THEN
                    INSERT INTO property_allowed_value (property_id, value)
                    VALUES
                        (v_prop_contract_type, 'subscription'),
                        (v_prop_contract_type, 'one_time'),
                        (v_prop_contract_type, 'retainer')
                    ON CONFLICT DO NOTHING;
                END IF;

                -- Bind client.client_lifetime_value + client_tenure_days
                IF v_client_type_id IS NOT NULL THEN
                    INSERT INTO entity_type_property (entity_type_id, property_id, is_required)
                    SELECT v_client_type_id, t.p_id, FALSE
                    FROM (VALUES (v_prop_client_ltv), (v_prop_client_tenure)) AS t(p_id)
                    WHERE t.p_id IS NOT NULL
                      AND NOT EXISTS (
                          SELECT 1 FROM entity_type_property
                          WHERE entity_type_id = v_client_type_id AND property_id = t.p_id
                      );
                END IF;

                -- Backfill existing contracts with contract_type = 'one_time' as safe default
                IF v_contract_type_id IS NOT NULL AND v_prop_contract_type IS NOT NULL THEN
                    INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                    SELECT e.id, v_prop_contract_type, 'one_time', NULL, NULL, NULL, NULL
                    FROM entity e
                    WHERE e.entity_type_id = v_contract_type_id
                    ON CONFLICT (entity_id, property_id) DO NOTHING;
                END IF;
            END $$;
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                DELETE FROM entity_type_property
                WHERE property_id IN (
                    SELECT id FROM property
                    WHERE name IN (
                        'expected_close', 'days_until_expected_close', 'historical_close_rate',
                        'contract_type', 'client_lifetime_value', 'client_tenure_days'
                    ) AND organization_id IS NULL
                );

                DELETE FROM entity_property_value
                WHERE property_id IN (
                    SELECT id FROM property
                    WHERE name IN (
                        'expected_close', 'days_until_expected_close', 'historical_close_rate',
                        'contract_type', 'client_lifetime_value', 'client_tenure_days'
                    ) AND organization_id IS NULL
                );

                DELETE FROM property
                WHERE organization_id IS NULL
                  AND name IN (
                      'expected_close', 'days_until_expected_close', 'historical_close_rate',
                      'contract_type', 'client_lifetime_value', 'client_tenure_days'
                  );
            END $$;
            """
        );
    }
}
