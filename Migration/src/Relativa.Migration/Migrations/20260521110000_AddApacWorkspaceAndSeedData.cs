using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

/// <summary>
/// Creates "APAC Sales Workspace" under org 1 (Relativa Global) and seeds:
///   10 clients, 20 deals (with deal_client links), 10 contacts,
///   15 tasks, 10 notes — all scoped to the APAC workspace via entity_workspace.
/// Idempotency guard: skips if workspace named 'APAC Sales Workspace' already exists.
/// </summary>
[DbContext(typeof(MigrationDbContext))]
[Migration("20260521110000_AddApacWorkspaceAndSeedData")]
public partial class AddApacWorkspaceAndSeedData : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                v_org_id        int;
                v_ws_id         int;
                v_dorian_id     int;
                v_ivan_id       int;
                v_lesya_id      int;
                v_ws_admin_role int;
                v_ws_mgr_role   int;
                v_ws_ana_role   int;

                v_deal_type_id    int;
                v_client_type_id  int;
                v_contact_type_id int;
                v_task_type_id    int;
                v_note_type_id    int;

                v_rel_deal_client_id    int;
                v_rel_client_contact_id int;
                v_rel_deal_task_id      int;
                v_rel_client_task_id    int;
                v_rel_deal_note_id      int;

                v_prop_company_name   int;
                v_prop_industry       int;
                v_prop_website        int;
                v_prop_annual_revenue int;
                v_prop_employee_count int;
                v_prop_client_status  int;
                v_prop_client_ltv     int;
                v_prop_client_tenure  int;

                v_prop_title          int;
                v_prop_deal_value     int;
                v_prop_deal_stage     int;
                v_prop_status         int;
                v_prop_deal_source    int;
                v_prop_priority       int;
                v_prop_expected_close int;
                v_prop_created_at     int;

                v_prop_first_name  int;
                v_prop_last_name   int;
                v_prop_email       int;
                v_prop_phone       int;
                v_prop_city        int;
                v_prop_country     int;
                v_prop_job_title   int;
                v_prop_department  int;

                v_prop_task_title    int;
                v_prop_task_status   int;
                v_prop_task_priority int;
                v_prop_task_type     int;
                v_prop_due_date      int;

                v_prop_note_content int;
                v_prop_note_date    int;

                v_new_id  int;
                v_client_ids int[] := ARRAY[]::int[];
                v_deal_ids   int[] := ARRAY[]::int[];
                i            int;

                -- client data (10)
                c_company   text[]    := ARRAY['AsiaTech Ventures','Pacific Finance Corp','MedCare Asia','SingaRetail Group','ManufactAsia Pte','SolarAsia Energy','AcademicBridge Asia','DataOcean Analytics','IndoFinance Solutions','PharmaAsia Global'];
                c_industry  text[]    := ARRAY['technology','finance','healthcare','retail','manufacturing','energy','education','technology','finance','healthcare'];
                c_cstatus   text[]    := ARRAY['active','active','active','prospect','active','prospect','active','at_risk','active','active'];
                c_ltv       decimal[] := ARRAY[950000,1400000,720000,280000,480000,160000,320000,540000,880000,1650000];
                c_emp       text[]    := ARRAY['201-1000','51-200','51-200','11-50','201-1000','11-50','11-50','51-200','51-200','1000+'];
                c_website   text[]    := ARRAY['asiatech.vc','pacificfinance.com.sg','medcareasia.health','singaRetail.sg','manufactasia.com','solarasia.energy','academicbridge.edu.sg','dataocean.ai','indofinance.id','pharmaasia.com'];
                c_annual    decimal[] := ARRAY[5100000,9800000,3600000,1200000,3100000,750000,1100000,2900000,4600000,14000000];
                c_tenure    int[]     := ARRAY[420,680,290,80,350,110,240,560,390,780];

                -- deal data (20)
                d_title text[] := ARRAY[
                    'Cloud Infrastructure APAC',  'Digital Banking Platform SG',  'EMR Modernisation KL',
                    'Omni-Channel Retail APAC',   'Factory Automation Jakarta',   'Solar Grid Management',
                    'LMS Platform Launch',        'DataOps Expansion',            'Treasury System Upgrade',
                    'HIPAA Equivalent Compliance','Supply Chain AI',              'Mobile Commerce Suite',
                    'Industrial IoT Rollout',     'Renewable Dashboard',          'Digital Curriculum',
                    'MLOps APAC Centre',          'Wealth Portal Singapore',      'Smart Inventory System',
                    'Patient Analytics SG',       'Enterprise AI Platform APAC'];

                d_client_idx int[] := ARRAY[1,2,3,4,5,6,7,8,9,3,5,4,5,6,7,8,9,4,3,1];

                d_stage text[] := ARRAY[
                    'Prospecting','Prospecting','Prospecting','Qualification','Qualification',
                    'Qualification','Proposal','Proposal','Proposal','Negotiation',
                    'Negotiation','Negotiation',NULL,NULL,'Prospecting',
                    'Qualification','Proposal','Negotiation','Prospecting','Negotiation'];

                d_status text[] := ARRAY[
                    'opened','opened','pending','opened','opened',
                    'pending','opened','closed','closed','opened',
                    'pending','revoked','closed','revoked','opened',
                    'opened','closed','opened','pending','closed'];

                d_source text[] := ARRAY[
                    'referral','cold_outreach','website','event','partner',
                    'referral','website','cold_outreach','event','partner',
                    'referral','cold_outreach','website','event','partner',
                    'referral','cold_outreach','website','event','partner'];

                d_priority text[] := ARRAY[
                    'high','high','medium','medium','high',
                    'low','high','high','medium','high',
                    'medium','low','high','medium','low',
                    'high','high','medium','low','high'];

                d_value decimal[] := ARRAY[
                    310000,520000,185000,140000,380000,
                    95000, 290000,430000,175000,560000,
                    220000,75000, 490000,60000, 105000,
                    370000,615000,195000,140000,480000];

                d_close_offset int[] := ARRAY[
                    60,90,30,-10,75,
                    120,45,-20,-50,80,
                    35,100,-30,-80,55,
                    70,-15,25,110,-40];

                d_created_ago int[] := ARRAY[
                    20,25,40,200,30,
                    15,35,85,95,22,
                    50,130,70,180,28,
                    18,45,60,90,55];
            BEGIN
                -- ── Idempotency guard ────────────────────────────────────────────
                IF EXISTS (SELECT 1 FROM workspaces WHERE name = 'APAC Sales Workspace') THEN
                    RAISE NOTICE 'APAC Sales Workspace already exists — skipping';
                    RETURN;
                END IF;

                -- ── Resolve references ───────────────────────────────────────────
                SELECT id INTO v_org_id FROM organizations WHERE id = 1 LIMIT 1;
                IF v_org_id IS NULL THEN
                    RAISE EXCEPTION 'Organization id=1 not found';
                END IF;

                SELECT id INTO v_dorian_id FROM users WHERE email = 'admin@relativa.com'   LIMIT 1;
                SELECT id INTO v_ivan_id   FROM users WHERE email = 'ivan.f@relativa.com'   LIMIT 1;
                SELECT id INTO v_lesya_id  FROM users WHERE email = 'lesya.u@relativa.com'  LIMIT 1;

                SELECT id INTO v_ws_admin_role FROM workspace_roles WHERE name = 'ws_admin'   AND workspace_id IS NULL LIMIT 1;
                SELECT id INTO v_ws_mgr_role   FROM workspace_roles WHERE name = 'ws_manager' AND workspace_id IS NULL LIMIT 1;
                SELECT id INTO v_ws_ana_role   FROM workspace_roles WHERE name = 'ws_analyst' AND workspace_id IS NULL LIMIT 1;

                SELECT id INTO v_deal_type_id    FROM entity_type WHERE name = 'deal'    LIMIT 1;
                SELECT id INTO v_client_type_id  FROM entity_type WHERE name = 'client'  LIMIT 1;
                SELECT id INTO v_contact_type_id FROM entity_type WHERE name = 'contact' LIMIT 1;
                SELECT id INTO v_task_type_id    FROM entity_type WHERE name = 'task'    LIMIT 1;
                SELECT id INTO v_note_type_id    FROM entity_type WHERE name = 'note'    LIMIT 1;

                SELECT id INTO v_rel_deal_client_id    FROM entity_relationship_type WHERE name = 'deal_client'    LIMIT 1;
                SELECT id INTO v_rel_client_contact_id FROM entity_relationship_type WHERE name = 'client_contact' LIMIT 1;
                SELECT id INTO v_rel_deal_task_id      FROM entity_relationship_type WHERE name = 'deal_task'      LIMIT 1;
                SELECT id INTO v_rel_client_task_id    FROM entity_relationship_type WHERE name = 'client_task'    LIMIT 1;
                SELECT id INTO v_rel_deal_note_id      FROM entity_relationship_type WHERE name = 'deal_note'      LIMIT 1;

                SELECT id INTO v_prop_company_name   FROM property WHERE name = 'company_name'         AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_industry       FROM property WHERE name = 'industry'              AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_website        FROM property WHERE name = 'website'               AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_annual_revenue FROM property WHERE name = 'annual_revenue'        AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_employee_count FROM property WHERE name = 'employee_count'        AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_client_status  FROM property WHERE name = 'client_status'         AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_client_ltv     FROM property WHERE name = 'client_lifetime_value' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_client_tenure  FROM property WHERE name = 'client_tenure_days'    AND organization_id IS NULL LIMIT 1;

                SELECT id INTO v_prop_title          FROM property WHERE name = 'title'          AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_deal_value     FROM property WHERE name = 'deal_value'     AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_deal_stage     FROM property WHERE name = 'deal_stage'     AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_status         FROM property WHERE name = 'status'         AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_deal_source    FROM property WHERE name = 'deal_source'    AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_priority       FROM property WHERE name = 'priority'       AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_expected_close FROM property WHERE name = 'expected_close' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_created_at     FROM property WHERE name = 'created_at'     AND organization_id IS NULL LIMIT 1;

                SELECT id INTO v_prop_first_name  FROM property WHERE name = 'first_name'   AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_last_name   FROM property WHERE name = 'last_name'    AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_email       FROM property WHERE name = 'email'        AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_phone       FROM property WHERE name = 'phone_number' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_city        FROM property WHERE name = 'city'         AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_country     FROM property WHERE name = 'country'      AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_job_title   FROM property WHERE name = 'job_title'    AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_department  FROM property WHERE name = 'department'   AND organization_id IS NULL LIMIT 1;

                SELECT id INTO v_prop_task_title    FROM property WHERE name = 'task_title'    AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_task_status   FROM property WHERE name = 'task_status'   AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_task_priority FROM property WHERE name = 'task_priority' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_task_type     FROM property WHERE name = 'task_type'     AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_due_date      FROM property WHERE name = 'due_date'      AND organization_id IS NULL LIMIT 1;

                SELECT id INTO v_prop_note_content FROM property WHERE name = 'note_content' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_note_date    FROM property WHERE name = 'note_date'    AND organization_id IS NULL LIMIT 1;

                -- ── Create workspace ─────────────────────────────────────────────
                INSERT INTO workspaces (name, organization_id, created_by_user_id, is_archived)
                VALUES ('APAC Sales Workspace', v_org_id, v_dorian_id, FALSE)
                RETURNING id INTO v_ws_id;

                -- ── Assign workspace roles ───────────────────────────────────────
                INSERT INTO user_role_workspace (user_id, workspace_id, ws_role_id, joined_at, is_archived)
                VALUES
                    (v_dorian_id, v_ws_id, v_ws_admin_role, NOW(), FALSE),
                    (v_ivan_id,   v_ws_id, v_ws_mgr_role,   NOW(), FALSE),
                    (v_lesya_id,  v_ws_id, v_ws_ana_role,   NOW(), FALSE);

                -- ── Seed 10 clients ──────────────────────────────────────────────
                FOR i IN 1..10 LOOP
                    INSERT INTO entity (entity_type_id, created_by_user_id, is_archived)
                    VALUES (v_client_type_id, v_dorian_id, FALSE)
                    RETURNING id INTO v_new_id;

                    INSERT INTO entity_workspace (entity_id, workspace_id) VALUES (v_new_id, v_ws_id);

                    INSERT INTO entity_property_value (entity_id, property_id, value_string) VALUES
                        (v_new_id, v_prop_company_name, c_company[i]),
                        (v_new_id, v_prop_industry,     c_industry[i]),
                        (v_new_id, v_prop_website,      c_website[i]),
                        (v_new_id, v_prop_client_status, c_cstatus[i]);

                    INSERT INTO entity_property_value (entity_id, property_id, value_decimal) VALUES
                        (v_new_id, v_prop_annual_revenue, c_annual[i]),
                        (v_new_id, v_prop_client_ltv,     c_ltv[i]);

                    INSERT INTO entity_property_value (entity_id, property_id, value_string) VALUES
                        (v_new_id, v_prop_employee_count, c_emp[i]);

                    IF v_prop_client_tenure IS NOT NULL THEN
                        INSERT INTO entity_property_value (entity_id, property_id, value_int)
                        VALUES (v_new_id, v_prop_client_tenure, c_tenure[i]);
                    END IF;

                    v_client_ids := v_client_ids || v_new_id;
                END LOOP;

                -- ── Seed 20 deals ────────────────────────────────────────────────
                FOR i IN 1..20 LOOP
                    INSERT INTO entity (entity_type_id, created_by_user_id, is_archived)
                    VALUES (v_deal_type_id, CASE WHEN i % 3 = 0 THEN v_ivan_id ELSE v_dorian_id END, FALSE)
                    RETURNING id INTO v_new_id;

                    INSERT INTO entity_workspace (entity_id, workspace_id) VALUES (v_new_id, v_ws_id);

                    INSERT INTO entity_property_value (entity_id, property_id, value_string) VALUES
                        (v_new_id, v_prop_title,       d_title[i]),
                        (v_new_id, v_prop_deal_source, d_source[i]),
                        (v_new_id, v_prop_priority,    d_priority[i]),
                        (v_new_id, v_prop_status,      d_status[i]);

                    IF d_stage[i] IS NOT NULL THEN
                        INSERT INTO entity_property_value (entity_id, property_id, value_string)
                        VALUES (v_new_id, v_prop_deal_stage, d_stage[i]);
                    END IF;

                    INSERT INTO entity_property_value (entity_id, property_id, value_decimal)
                    VALUES (v_new_id, v_prop_deal_value, d_value[i]);

                    IF v_prop_expected_close IS NOT NULL THEN
                        INSERT INTO entity_property_value (entity_id, property_id, value_date)
                        VALUES (v_new_id, v_prop_expected_close, CURRENT_DATE + d_close_offset[i]);
                    END IF;

                    IF v_prop_created_at IS NOT NULL THEN
                        INSERT INTO entity_property_value (entity_id, property_id, value_date)
                        VALUES (v_new_id, v_prop_created_at, CURRENT_DATE - d_created_ago[i]);
                    END IF;

                    -- Link deal → client
                    IF v_rel_deal_client_id IS NOT NULL THEN
                        INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                        VALUES (v_new_id, v_client_ids[d_client_idx[i]], v_rel_deal_client_id);
                    END IF;

                    v_deal_ids := v_deal_ids || v_new_id;
                END LOOP;

                -- ── Seed 10 contacts ─────────────────────────────────────────────
                DECLARE
                    con_first  text[] := ARRAY['Mei','Hiroshi','Priya','Siti','Chen','Ravi','Aiko','Budi','Min','Fatima'];
                    con_last   text[] := ARRAY['Tanaka','Yamamoto','Patel','Binte','Wei','Kumar','Nakamura','Santoso','Li','Al-Hassan'];
                    con_email  text[] := ARRAY['mei.tanaka@asiatech.vc','h.yamamoto@pacificfinance.com.sg','priya.patel@medcareasia.health','siti@singaRetail.sg','chen.wei@manufactasia.com','ravi@solarasia.energy','aiko@academicbridge.edu.sg','budi@dataocean.ai','min.li@indofinance.id','fatima@pharmaasia.com'];
                    con_city   text[] := ARRAY['Tokyo','Singapore','Mumbai','Singapore','Jakarta','Chennai','Osaka','Jakarta','Surabaya','Kuala Lumpur'];
                    con_country text[] := ARRAY['Japan','Singapore','India','Singapore','Indonesia','India','Japan','Indonesia','Indonesia','Malaysia'];
                    con_role   text[] := ARRAY['CTO','CFO','Medical Director','VP Retail','COO','CEO','Head of Education','Data Lead','Finance Director','R&D Director'];
                    con_dept   text[] := ARRAY['Technology','Finance','Operations','Commercial','Operations','Executive','Education','Analytics','Finance','Research'];
                    con_client_idx int[] := ARRAY[1,2,3,4,5,6,7,8,9,10];
                BEGIN
                    FOR i IN 1..10 LOOP
                        INSERT INTO entity (entity_type_id, created_by_user_id, is_archived)
                        VALUES (v_contact_type_id, v_ivan_id, FALSE)
                        RETURNING id INTO v_new_id;

                        INSERT INTO entity_workspace (entity_id, workspace_id) VALUES (v_new_id, v_ws_id);

                        INSERT INTO entity_property_value (entity_id, property_id, value_string) VALUES
                            (v_new_id, v_prop_first_name, con_first[i]),
                            (v_new_id, v_prop_last_name,  con_last[i]),
                            (v_new_id, v_prop_email,      con_email[i]),
                            (v_new_id, v_prop_city,       con_city[i]),
                            (v_new_id, v_prop_country,    con_country[i]),
                            (v_new_id, v_prop_job_title,  con_role[i]),
                            (v_new_id, v_prop_department, con_dept[i]);

                        -- Link contact → client
                        IF v_rel_client_contact_id IS NOT NULL THEN
                            INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                            VALUES (v_client_ids[con_client_idx[i]], v_new_id, v_rel_client_contact_id);
                        END IF;
                    END LOOP;
                END;

                -- ── Seed 15 tasks ────────────────────────────────────────────────
                DECLARE
                    t_title    text[]    := ARRAY['Discovery call SG','Demo for Pacific Finance','Follow up MedCare','Proposal review SingaRetail','Factory site visit','Solar assessment','LMS pilot kickoff','DataOps scoping','Treasury deep-dive','Compliance walkthrough','AI workshop Jakarta','Commerce demo','IoT feasibility','Energy audit','Curriculum review'];
                    t_status   text[]    := ARRAY['done','done','in_progress','todo','done','in_progress','done','todo','in_progress','todo','done','in_progress','todo','done','todo'];
                    t_priority text[]    := ARRAY['high','high','medium','medium','high','low','high','medium','high','medium','high','medium','low','medium','low'];
                    t_type     text[]    := ARRAY['call','demo','follow_up','meeting','meeting','call','meeting','call','demo','meeting','meeting','demo','meeting','call','meeting'];
                    t_due_off  int[]     := ARRAY[-30,-20,-5,15,-40,-10,-25,20,-15,30,-35,10,25,-20,40];
                    t_deal_idx int[]     := ARRAY[1,2,3,4,5,6,7,8,9,10,11,12,13,14,15];
                BEGIN
                    FOR i IN 1..15 LOOP
                        INSERT INTO entity (entity_type_id, created_by_user_id, is_archived)
                        VALUES (v_task_type_id, CASE WHEN i % 2 = 0 THEN v_lesya_id ELSE v_ivan_id END, FALSE)
                        RETURNING id INTO v_new_id;

                        INSERT INTO entity_workspace (entity_id, workspace_id) VALUES (v_new_id, v_ws_id);

                        INSERT INTO entity_property_value (entity_id, property_id, value_string) VALUES
                            (v_new_id, v_prop_task_title,    t_title[i]),
                            (v_new_id, v_prop_task_status,   t_status[i]),
                            (v_new_id, v_prop_task_priority, t_priority[i]),
                            (v_new_id, v_prop_task_type,     t_type[i]);

                        IF v_prop_due_date IS NOT NULL THEN
                            INSERT INTO entity_property_value (entity_id, property_id, value_date)
                            VALUES (v_new_id, v_prop_due_date, CURRENT_DATE + t_due_off[i]);
                        END IF;

                        -- Link task → deal
                        IF v_rel_deal_task_id IS NOT NULL AND t_deal_idx[i] <= array_length(v_deal_ids, 1) THEN
                            INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                            VALUES (v_deal_ids[t_deal_idx[i]], v_new_id, v_rel_deal_task_id);
                        END IF;
                    END LOOP;
                END;

                -- ── Seed 10 notes ────────────────────────────────────────────────
                DECLARE
                    n_content text[] := ARRAY[
                        'Initial call with AsiaTech — strong interest in cloud migration.',
                        'Pacific Finance CFO confirmed budget approved for treasury system.',
                        'MedCare Asia needs HIPAA-equivalent compliance by Q3.',
                        'SingaRetail Group evaluating 3 vendors — we are shortlisted.',
                        'ManufactAsia Pte wants phased rollout starting Q2.',
                        'Solar assessment scheduled — client prefers on-premise deployment.',
                        'AcademicBridge piloting LMS with 200 students next month.',
                        'DataOcean identified 3 pain points in current data pipeline.',
                        'IndoFinance looking to integrate with existing Oracle ERP.',
                        'PharmaAsia confirmed APAC expansion — highest priority deal.'];
                    n_deal_idx int[] := ARRAY[1,2,3,4,5,6,7,8,9,20];
                BEGIN
                    FOR i IN 1..10 LOOP
                        INSERT INTO entity (entity_type_id, created_by_user_id, is_archived)
                        VALUES (v_note_type_id, v_dorian_id, FALSE)
                        RETURNING id INTO v_new_id;

                        INSERT INTO entity_workspace (entity_id, workspace_id) VALUES (v_new_id, v_ws_id);

                        INSERT INTO entity_property_value (entity_id, property_id, value_string)
                        VALUES (v_new_id, v_prop_note_content, n_content[i]);

                        IF v_prop_note_date IS NOT NULL THEN
                            INSERT INTO entity_property_value (entity_id, property_id, value_date)
                            VALUES (v_new_id, v_prop_note_date, CURRENT_DATE - (i * 3));
                        END IF;

                        -- Link note → deal
                        IF v_rel_deal_note_id IS NOT NULL AND n_deal_idx[i] <= array_length(v_deal_ids, 1) THEN
                            INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                            VALUES (v_deal_ids[n_deal_idx[i]], v_new_id, v_rel_deal_note_id);
                        END IF;
                    END LOOP;
                END;

            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                v_ws_id int;
                v_entity_ids int[];
            BEGIN
                SELECT id INTO v_ws_id FROM workspaces WHERE name = 'APAC Sales Workspace' LIMIT 1;
                IF v_ws_id IS NULL THEN RETURN; END IF;

                -- Collect entity IDs in this workspace
                SELECT ARRAY_AGG(DISTINCT entity_id) INTO v_entity_ids
                FROM entity_workspace WHERE workspace_id = v_ws_id;

                IF v_entity_ids IS NOT NULL THEN
                    DELETE FROM entity_relationship
                    WHERE source_entity_id = ANY(v_entity_ids)
                       OR target_entity_id = ANY(v_entity_ids);

                    DELETE FROM entity_property_value WHERE entity_id = ANY(v_entity_ids);
                    DELETE FROM entity_workspace WHERE entity_id = ANY(v_entity_ids);
                    DELETE FROM entity WHERE id = ANY(v_entity_ids);
                END IF;

                DELETE FROM user_role_workspace WHERE workspace_id = v_ws_id;
                DELETE FROM workspaces WHERE id = v_ws_id;
            END $$;
            """);
    }
}
