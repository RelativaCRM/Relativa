using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

[DbContext(typeof(MigrationDbContext))]
[Migration("20260517100000_AddProductionCrmSchema")]
public partial class AddProductionCrmSchema : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                v_deal_id     int;
                v_client_id   int;
                v_contact_id  int;
                v_task_id     int;
                v_note_id     int;

                v_prop_title          int;
                v_prop_deal_stage     int;
                v_prop_deal_source    int;
                v_prop_priority       int;
                v_prop_company_name   int;
                v_prop_industry       int;
                v_prop_website        int;
                v_prop_annual_revenue int;
                v_prop_employee_count int;
                v_prop_client_status  int;
                v_prop_job_title      int;
                v_prop_department     int;
                v_prop_task_title     int;
                v_prop_task_status    int;
                v_prop_task_priority  int;
                v_prop_task_type      int;
                v_prop_due_date       int;
                v_prop_note_content   int;
                v_prop_note_date      int;

                v_prop_first_name     int;
                v_prop_last_name      int;
                v_prop_email          int;
                v_prop_phone          int;
                v_prop_city           int;
                v_prop_country        int;
            BEGIN
                SELECT id INTO v_deal_id   FROM entity_type WHERE name = 'deal'   LIMIT 1;
                SELECT id INTO v_client_id FROM entity_type WHERE name = 'client' LIMIT 1;

                -- ── New entity types ─────────────────────────────────────────────
                INSERT INTO entity_type (name, is_standalone)
                SELECT et_name, et_standalone
                FROM (VALUES
                    ('contact', TRUE),
                    ('task',    TRUE),
                    ('note',    FALSE)
                ) AS v(et_name, et_standalone)
                WHERE NOT EXISTS (SELECT 1 FROM entity_type WHERE name = v.et_name);

                SELECT id INTO v_contact_id FROM entity_type WHERE name = 'contact' LIMIT 1;
                SELECT id INTO v_task_id    FROM entity_type WHERE name = 'task'    LIMIT 1;
                SELECT id INTO v_note_id    FROM entity_type WHERE name = 'note'    LIMIT 1;

                -- ── New global properties ────────────────────────────────────────
                INSERT INTO property (name, data_type, organization_id, is_readonly)
                SELECT p_name, p_type, NULL, p_ro
                FROM (VALUES
                    ('title',          'String',  false),
                    ('deal_stage',     'String',  false),
                    ('deal_source',    'String',  false),
                    ('priority',       'String',  false),
                    ('company_name',   'String',  false),
                    ('industry',       'String',  false),
                    ('website',        'String',  false),
                    ('annual_revenue', 'Decimal', false),
                    ('employee_count', 'String',  false),
                    ('client_status',  'String',  false),
                    ('job_title',      'String',  false),
                    ('department',     'String',  false),
                    ('task_title',     'String',  false),
                    ('task_status',    'String',  false),
                    ('task_priority',  'String',  false),
                    ('task_type',      'String',  false),
                    ('due_date',       'Date',    false),
                    ('note_content',   'String',  false),
                    ('note_date',      'Date',    false)
                ) AS v(p_name, p_type, p_ro)
                WHERE NOT EXISTS (
                    SELECT 1 FROM property p WHERE p.name = v.p_name AND p.organization_id IS NULL
                );

                -- ── Resolve property IDs ─────────────────────────────────────────
                SELECT id INTO v_prop_title          FROM property WHERE name = 'title'          AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_deal_stage     FROM property WHERE name = 'deal_stage'     AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_deal_source    FROM property WHERE name = 'deal_source'    AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_priority       FROM property WHERE name = 'priority'       AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_company_name   FROM property WHERE name = 'company_name'   AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_industry       FROM property WHERE name = 'industry'       AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_website        FROM property WHERE name = 'website'        AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_annual_revenue FROM property WHERE name = 'annual_revenue' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_employee_count FROM property WHERE name = 'employee_count' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_client_status  FROM property WHERE name = 'client_status'  AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_job_title      FROM property WHERE name = 'job_title'      AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_department     FROM property WHERE name = 'department'     AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_task_title     FROM property WHERE name = 'task_title'     AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_task_status    FROM property WHERE name = 'task_status'    AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_task_priority  FROM property WHERE name = 'task_priority'  AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_task_type      FROM property WHERE name = 'task_type'      AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_due_date       FROM property WHERE name = 'due_date'       AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_note_content   FROM property WHERE name = 'note_content'   AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_note_date      FROM property WHERE name = 'note_date'      AND organization_id IS NULL LIMIT 1;

                SELECT id INTO v_prop_first_name FROM property WHERE name = 'first_name'   AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_last_name  FROM property WHERE name = 'last_name'    AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_email      FROM property WHERE name = 'email'        AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_phone      FROM property WHERE name = 'phone_number' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_city       FROM property WHERE name = 'city'         AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_country    FROM property WHERE name = 'country'      AND organization_id IS NULL LIMIT 1;

                -- ── Allowed values ───────────────────────────────────────────────
                -- deal_stage
                INSERT INTO property_allowed_value (property_id, value)
                SELECT v_prop_deal_stage, v
                FROM (VALUES ('Prospecting'),('Qualification'),('Proposal'),('Negotiation')) AS t(v)
                ON CONFLICT DO NOTHING;

                -- deal_source
                INSERT INTO property_allowed_value (property_id, value)
                SELECT v_prop_deal_source, v
                FROM (VALUES ('cold_outreach'),('referral'),('website'),('event'),('partner')) AS t(v)
                ON CONFLICT DO NOTHING;

                -- priority
                INSERT INTO property_allowed_value (property_id, value)
                SELECT v_prop_priority, v
                FROM (VALUES ('high'),('medium'),('low')) AS t(v)
                ON CONFLICT DO NOTHING;

                -- industry
                INSERT INTO property_allowed_value (property_id, value)
                SELECT v_prop_industry, v
                FROM (VALUES ('technology'),('finance'),('healthcare'),('retail'),('manufacturing'),('energy'),('education'),('other')) AS t(v)
                ON CONFLICT DO NOTHING;

                -- employee_count
                INSERT INTO property_allowed_value (property_id, value)
                SELECT v_prop_employee_count, v
                FROM (VALUES ('1-10'),('11-50'),('51-200'),('201-1000'),('1000+')) AS t(v)
                ON CONFLICT DO NOTHING;

                -- client_status
                INSERT INTO property_allowed_value (property_id, value)
                SELECT v_prop_client_status, v
                FROM (VALUES ('lead'),('prospect'),('active'),('at_risk'),('churned')) AS t(v)
                ON CONFLICT DO NOTHING;

                -- task_status
                INSERT INTO property_allowed_value (property_id, value)
                SELECT v_prop_task_status, v
                FROM (VALUES ('todo'),('in_progress'),('done'),('cancelled')) AS t(v)
                ON CONFLICT DO NOTHING;

                -- task_priority (reuse same values as priority)
                INSERT INTO property_allowed_value (property_id, value)
                SELECT v_prop_task_priority, v
                FROM (VALUES ('high'),('medium'),('low')) AS t(v)
                ON CONFLICT DO NOTHING;

                -- task_type
                INSERT INTO property_allowed_value (property_id, value)
                SELECT v_prop_task_type, v
                FROM (VALUES ('call'),('meeting'),('email'),('follow_up'),('demo')) AS t(v)
                ON CONFLICT DO NOTHING;

                -- ── Bind properties to entity types ──────────────────────────────
                -- deal: title, deal_stage, deal_source, priority
                IF v_deal_id IS NOT NULL THEN
                    INSERT INTO entity_type_property (entity_type_id, property_id, is_required)
                    SELECT v_deal_id, p_id, FALSE
                    FROM (VALUES (v_prop_title),(v_prop_deal_stage),(v_prop_deal_source),(v_prop_priority)) AS t(p_id)
                    WHERE t.p_id IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM entity_type_property WHERE entity_type_id = v_deal_id AND property_id = t.p_id);
                END IF;

                -- client: company_name, industry, website, annual_revenue, employee_count, client_status
                IF v_client_id IS NOT NULL THEN
                    INSERT INTO entity_type_property (entity_type_id, property_id, is_required)
                    SELECT v_client_id, p_id, FALSE
                    FROM (VALUES (v_prop_company_name),(v_prop_industry),(v_prop_website),(v_prop_annual_revenue),(v_prop_employee_count),(v_prop_client_status)) AS t(p_id)
                    WHERE t.p_id IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM entity_type_property WHERE entity_type_id = v_client_id AND property_id = t.p_id);
                END IF;

                -- contact: first_name (required), last_name (required), email, phone, city, country, job_title, department
                IF v_contact_id IS NOT NULL THEN
                    INSERT INTO entity_type_property (entity_type_id, property_id, is_required)
                    SELECT v_contact_id, p_id, req
                    FROM (VALUES
                        (v_prop_first_name, TRUE),(v_prop_last_name, TRUE),
                        (v_prop_email, FALSE),(v_prop_phone, FALSE),
                        (v_prop_city, FALSE),(v_prop_country, FALSE),
                        (v_prop_job_title, FALSE),(v_prop_department, FALSE)
                    ) AS t(p_id, req)
                    WHERE t.p_id IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM entity_type_property WHERE entity_type_id = v_contact_id AND property_id = t.p_id);
                END IF;

                -- task: task_title (required), task_status (required), task_priority, task_type, due_date
                IF v_task_id IS NOT NULL THEN
                    INSERT INTO entity_type_property (entity_type_id, property_id, is_required)
                    SELECT v_task_id, p_id, req
                    FROM (VALUES
                        (v_prop_task_title, TRUE),(v_prop_task_status, TRUE),
                        (v_prop_task_priority, FALSE),(v_prop_task_type, FALSE),
                        (v_prop_due_date, FALSE)
                    ) AS t(p_id, req)
                    WHERE t.p_id IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM entity_type_property WHERE entity_type_id = v_task_id AND property_id = t.p_id);
                END IF;

                -- note: note_content (required), note_date (required)
                IF v_note_id IS NOT NULL THEN
                    INSERT INTO entity_type_property (entity_type_id, property_id, is_required)
                    SELECT v_note_id, p_id, TRUE
                    FROM (VALUES (v_prop_note_content),(v_prop_note_date)) AS t(p_id)
                    WHERE t.p_id IS NOT NULL
                      AND NOT EXISTS (SELECT 1 FROM entity_type_property WHERE entity_type_id = v_note_id AND property_id = t.p_id);
                END IF;

                -- ── New relationship types ────────────────────────────────────────
                -- client → contact (one client has many contacts)
                INSERT INTO entity_relationship_type (name, source_entity_type_id, target_entity_type_id, is_required, relationship_cardinality)
                SELECT 'client_contact', v_client_id, v_contact_id, FALSE, 'one_to_many'
                WHERE v_client_id IS NOT NULL AND v_contact_id IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM entity_relationship_type WHERE name = 'client_contact' AND source_entity_type_id = v_client_id AND target_entity_type_id = v_contact_id);

                -- deal → contact (many deals link to many contacts)
                INSERT INTO entity_relationship_type (name, source_entity_type_id, target_entity_type_id, is_required, relationship_cardinality)
                SELECT 'deal_contact', v_deal_id, v_contact_id, FALSE, 'many_to_many'
                WHERE v_deal_id IS NOT NULL AND v_contact_id IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM entity_relationship_type WHERE name = 'deal_contact' AND source_entity_type_id = v_deal_id AND target_entity_type_id = v_contact_id);

                -- deal → task
                INSERT INTO entity_relationship_type (name, source_entity_type_id, target_entity_type_id, is_required, relationship_cardinality)
                SELECT 'deal_task', v_deal_id, v_task_id, FALSE, 'one_to_many'
                WHERE v_deal_id IS NOT NULL AND v_task_id IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM entity_relationship_type WHERE name = 'deal_task' AND source_entity_type_id = v_deal_id AND target_entity_type_id = v_task_id);

                -- client → task
                INSERT INTO entity_relationship_type (name, source_entity_type_id, target_entity_type_id, is_required, relationship_cardinality)
                SELECT 'client_task', v_client_id, v_task_id, FALSE, 'one_to_many'
                WHERE v_client_id IS NOT NULL AND v_task_id IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM entity_relationship_type WHERE name = 'client_task' AND source_entity_type_id = v_client_id AND target_entity_type_id = v_task_id);

                -- deal → note
                INSERT INTO entity_relationship_type (name, source_entity_type_id, target_entity_type_id, is_required, relationship_cardinality)
                SELECT 'deal_note', v_deal_id, v_note_id, FALSE, 'one_to_many'
                WHERE v_deal_id IS NOT NULL AND v_note_id IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM entity_relationship_type WHERE name = 'deal_note' AND source_entity_type_id = v_deal_id AND target_entity_type_id = v_note_id);

                -- client → note
                INSERT INTO entity_relationship_type (name, source_entity_type_id, target_entity_type_id, is_required, relationship_cardinality)
                SELECT 'client_note', v_client_id, v_note_id, FALSE, 'one_to_many'
                WHERE v_client_id IS NOT NULL AND v_note_id IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM entity_relationship_type WHERE name = 'client_note' AND source_entity_type_id = v_client_id AND target_entity_type_id = v_note_id);
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
                v_deal_id   int;
                v_client_id int;
            BEGIN
                SELECT id INTO v_deal_id   FROM entity_type WHERE name = 'deal'   LIMIT 1;
                SELECT id INTO v_client_id FROM entity_type WHERE name = 'client' LIMIT 1;

                DELETE FROM entity_relationship_type
                WHERE name IN ('client_contact','deal_contact','deal_task','client_task','deal_note','client_note');

                DELETE FROM entity_type_property
                WHERE entity_type_id IN (SELECT id FROM entity_type WHERE name IN ('contact','task','note'));

                DELETE FROM entity_type WHERE name IN ('contact','task','note');

                IF v_deal_id IS NOT NULL THEN
                    DELETE FROM entity_type_property
                    WHERE entity_type_id = v_deal_id
                      AND property_id IN (
                          SELECT id FROM property
                          WHERE name IN ('title','deal_stage','deal_source','priority')
                            AND organization_id IS NULL
                      );
                END IF;

                IF v_client_id IS NOT NULL THEN
                    DELETE FROM entity_type_property
                    WHERE entity_type_id = v_client_id
                      AND property_id IN (
                          SELECT id FROM property
                          WHERE name IN ('company_name','industry','website','annual_revenue','employee_count','client_status')
                            AND organization_id IS NULL
                      );
                END IF;

                DELETE FROM property
                WHERE organization_id IS NULL
                  AND name IN (
                      'title','deal_stage','deal_source','priority',
                      'company_name','industry','website','annual_revenue','employee_count','client_status',
                      'job_title','department',
                      'task_title','task_status','task_priority','task_type','due_date',
                      'note_content','note_date'
                  );
            END $$;
            """
        );
    }
}
