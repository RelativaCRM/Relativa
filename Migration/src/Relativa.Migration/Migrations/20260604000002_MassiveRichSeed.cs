using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

/// <summary>
/// Seeds a massive, realistic dataset for comprehensive exploration:
///   - 12 new users (15 total), each belonging to multiple organizations
///   - 3 new organizations (5 total) with settings
///   - 7 new workspaces (10 total) with membership assignments
///   - ~630 new entities: 15 clients + 30 deals (each with deal_analysis + contract)
///     + 15 contacts + 20 tasks + 10 notes per workspace × 7 workspaces
///   - ~460 audit log rows spread across a 90-day window
///   - Organization invitations and join requests
///
/// Idempotency guard: skips if users table already has more than 7 rows.
/// </summary>
[DbContext(typeof(MigrationDbContext))]
[Migration("20260604000002_MassiveRichSeed")]
public partial class MassiveRichSeed : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Demo1234!");

        // ── Part A: Users, Organizations, Workspaces, Memberships ────────────
        // Each INSERT uses ON CONFLICT DO NOTHING for idempotency.
        migrationBuilder.Sql(
            $"""
            DO $$
            DECLARE
                v_pw text := '{passwordHash}';

                -- New user IDs
                v_alice_id   int; v_james_id  int; v_sarah_id  int;
                v_michael_id int; v_laura_id  int; v_david_id  int;
                v_emma_id    int; v_oliver_id int; v_sophie_id int;
                v_henry_id   int; v_claire_id int; v_ryan_id   int;

                -- Existing user IDs
                v_dorian_id int; v_ivan_id int; v_lesya_id int;

                -- New org IDs
                v_org_gv  int; -- Global Ventures Ltd
                v_org_fe  int; -- FinEdge Capital
                v_org_hc  int; -- HealthCore Systems

                -- Existing org IDs
                v_org1 int; v_org2 int;

                -- New workspace IDs
                v_ws_emea   int; v_ws_apac_gv int; v_ws_strat  int;
                v_ws_inst   int; v_ws_retail  int;
                v_ws_hosp   int; v_ws_pharma  int;

                -- System roles (shared, workspace_id IS NULL)
                v_ws_admin_role   int; v_ws_mgr_role    int;
                v_ws_analyst_role int; v_ws_member_role int;
                v_org_owner_role  int; v_org_admin_role int; v_org_member_role int;

            BEGIN
                -- Resolve existing users
                SELECT id INTO v_dorian_id FROM users WHERE email = 'admin@relativa.com'  LIMIT 1;
                SELECT id INTO v_ivan_id   FROM users WHERE email = 'ivan.f@relativa.com'  LIMIT 1;
                SELECT id INTO v_lesya_id  FROM users WHERE email = 'lesya.u@relativa.com' LIMIT 1;

                -- Resolve existing orgs
                SELECT id INTO v_org1 FROM organizations WHERE id = 1 LIMIT 1;
                SELECT id INTO v_org2 FROM organizations WHERE id = 2 LIMIT 1;

                -- Resolve system roles
                SELECT id INTO v_ws_admin_role   FROM workspace_roles WHERE name = 'ws_admin'   AND workspace_id IS NULL LIMIT 1;
                SELECT id INTO v_ws_mgr_role     FROM workspace_roles WHERE name = 'ws_manager' AND workspace_id IS NULL LIMIT 1;
                SELECT id INTO v_ws_analyst_role FROM workspace_roles WHERE name = 'ws_analyst' AND workspace_id IS NULL LIMIT 1;
                SELECT id INTO v_ws_member_role  FROM workspace_roles WHERE name = 'ws_member'  AND workspace_id IS NULL LIMIT 1;
                SELECT id INTO v_org_owner_role  FROM organization_roles WHERE name = 'org_owner'  AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_org_admin_role  FROM organization_roles WHERE name = 'org_admin'  AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_org_member_role FROM organization_roles WHERE name = 'org_member' AND organization_id IS NULL LIMIT 1;

                -- ── Insert new users ─────────────────────────────────────────────
                INSERT INTO users (first_name, last_name, email, password, created_at, is_archived)
                VALUES
                    ('Alice',   'Morgan',  'alice.morgan@relativa.com',   v_pw, NOW() - INTERVAL '85 days', FALSE),
                    ('James',   'Carter',  'james.carter@relativa.com',   v_pw, NOW() - INTERVAL '80 days', FALSE),
                    ('Sarah',   'Brooks',  'sarah.brooks@relativa.com',   v_pw, NOW() - INTERVAL '75 days', FALSE),
                    ('Michael', 'Hayes',   'michael.hayes@relativa.com',  v_pw, NOW() - INTERVAL '70 days', FALSE),
                    ('Laura',   'Scott',   'laura.scott@relativa.com',    v_pw, NOW() - INTERVAL '65 days', FALSE),
                    ('David',   'Chen',    'david.chen@relativa.com',     v_pw, NOW() - INTERVAL '60 days', FALSE),
                    ('Emma',    'Wilson',  'emma.wilson@relativa.com',    v_pw, NOW() - INTERVAL '55 days', FALSE),
                    ('Oliver',  'Grant',   'oliver.grant@relativa.com',   v_pw, NOW() - INTERVAL '50 days', FALSE),
                    ('Sophie',  'Lane',    'sophie.lane@relativa.com',    v_pw, NOW() - INTERVAL '45 days', FALSE),
                    ('Henry',   'Ford',    'henry.ford@relativa.com',     v_pw, NOW() - INTERVAL '40 days', FALSE),
                    ('Claire',  'West',    'claire.west@relativa.com',    v_pw, NOW() - INTERVAL '35 days', FALSE),
                    ('Ryan',    'Bishop',  'ryan.bishop@relativa.com',    v_pw, NOW() - INTERVAL '30 days', FALSE)
                ON CONFLICT DO NOTHING;

                SELECT id INTO v_alice_id   FROM users WHERE email = 'alice.morgan@relativa.com'   LIMIT 1;
                SELECT id INTO v_james_id   FROM users WHERE email = 'james.carter@relativa.com'   LIMIT 1;
                SELECT id INTO v_sarah_id   FROM users WHERE email = 'sarah.brooks@relativa.com'   LIMIT 1;
                SELECT id INTO v_michael_id FROM users WHERE email = 'michael.hayes@relativa.com'  LIMIT 1;
                SELECT id INTO v_laura_id   FROM users WHERE email = 'laura.scott@relativa.com'    LIMIT 1;
                SELECT id INTO v_david_id   FROM users WHERE email = 'david.chen@relativa.com'     LIMIT 1;
                SELECT id INTO v_emma_id    FROM users WHERE email = 'emma.wilson@relativa.com'    LIMIT 1;
                SELECT id INTO v_oliver_id  FROM users WHERE email = 'oliver.grant@relativa.com'   LIMIT 1;
                SELECT id INTO v_sophie_id  FROM users WHERE email = 'sophie.lane@relativa.com'    LIMIT 1;
                SELECT id INTO v_henry_id   FROM users WHERE email = 'henry.ford@relativa.com'     LIMIT 1;
                SELECT id INTO v_claire_id  FROM users WHERE email = 'claire.west@relativa.com'    LIMIT 1;
                SELECT id INTO v_ryan_id    FROM users WHERE email = 'ryan.bishop@relativa.com'    LIMIT 1;

                -- ── Insert new organizations ─────────────────────────────────────
                INSERT INTO organizations (name, is_archived)
                SELECT name, FALSE FROM (VALUES
                    ('Global Ventures Ltd'),
                    ('FinEdge Capital'),
                    ('HealthCore Systems')
                ) AS t(name)
                WHERE NOT EXISTS (SELECT 1 FROM organizations WHERE organizations.name = t.name);

                SELECT id INTO v_org_gv FROM organizations WHERE name = 'Global Ventures Ltd' LIMIT 1;
                SELECT id INTO v_org_fe FROM organizations WHERE name = 'FinEdge Capital'      LIMIT 1;
                SELECT id INTO v_org_hc FROM organizations WHERE name = 'HealthCore Systems'   LIMIT 1;

                -- Organization settings
                INSERT INTO organization_settings (organization_id, join_policy, description)
                VALUES
                    (v_org_gv, 'open',         'Global enterprise sales and partnerships organization'),
                    (v_org_fe, 'invite_only',   'Finance-focused capital and investment organization'),
                    (v_org_hc, 'open',          'Healthcare technology and life sciences organization')
                ON CONFLICT (organization_id) DO NOTHING;

                -- ── Organization memberships ─────────────────────────────────────
                -- alice: owner of Global Ventures, admin of Relativa Global
                -- james: owner of FinEdge, member of Global Ventures
                -- sarah: owner of HealthCore, admin of Global Ventures
                -- michael, laura, david: member of Relativa Global + Global Ventures
                -- emma, oliver: member of FinEdge Capital
                -- sophie, henry: member of HealthCore Systems
                -- claire, ryan: member across all 3 new orgs
                -- dorian is already owner of Relativa Global; add to Global Ventures as admin
                INSERT INTO user_role_organization (user_id, organization_id, org_role_id, joined_at, is_archived)
                SELECT u, o, r, NOW() - INTERVAL '80 days', FALSE
                FROM (VALUES
                    (v_alice_id,   v_org_gv,  v_org_owner_role),
                    (v_alice_id,   v_org1,    v_org_admin_role),
                    (v_james_id,   v_org_fe,  v_org_owner_role),
                    (v_james_id,   v_org_gv,  v_org_member_role),
                    (v_sarah_id,   v_org_hc,  v_org_owner_role),
                    (v_sarah_id,   v_org_gv,  v_org_admin_role),
                    (v_michael_id, v_org1,    v_org_member_role),
                    (v_michael_id, v_org_gv,  v_org_member_role),
                    (v_laura_id,   v_org1,    v_org_member_role),
                    (v_laura_id,   v_org_gv,  v_org_member_role),
                    (v_david_id,   v_org1,    v_org_member_role),
                    (v_david_id,   v_org_gv,  v_org_member_role),
                    (v_emma_id,    v_org_fe,  v_org_member_role),
                    (v_oliver_id,  v_org_fe,  v_org_member_role),
                    (v_sophie_id,  v_org_hc,  v_org_member_role),
                    (v_henry_id,   v_org_hc,  v_org_member_role),
                    (v_claire_id,  v_org_gv,  v_org_member_role),
                    (v_claire_id,  v_org_fe,  v_org_member_role),
                    (v_claire_id,  v_org_hc,  v_org_member_role),
                    (v_ryan_id,    v_org_gv,  v_org_member_role),
                    (v_ryan_id,    v_org_fe,  v_org_member_role),
                    (v_ryan_id,    v_org_hc,  v_org_member_role),
                    (v_dorian_id,  v_org_gv,  v_org_admin_role),
                    (v_ivan_id,    v_org_gv,  v_org_member_role)
                ) AS t(u, o, r)
                WHERE t.u IS NOT NULL AND t.o IS NOT NULL AND t.r IS NOT NULL
                ON CONFLICT (user_id, organization_id) DO NOTHING;

                -- ── Create workspaces ────────────────────────────────────────────
                INSERT INTO workspaces (name, organization_id, created_by_user_id, is_archived)
                SELECT ws_name, ws_org, ws_owner, FALSE
                FROM (VALUES
                    ('Enterprise Sales EMEA',   v_org_gv, v_alice_id),
                    ('Enterprise Sales APAC',   v_org_gv, v_alice_id),
                    ('Strategic Accounts',      v_org_gv, v_sarah_id),
                    ('Institutional Sales',     v_org_fe, v_james_id),
                    ('Retail Investment',       v_org_fe, v_james_id),
                    ('Hospital Accounts',       v_org_hc, v_sarah_id),
                    ('Pharma Partnerships',     v_org_hc, v_henry_id)
                ) AS t(ws_name, ws_org, ws_owner)
                WHERE NOT EXISTS (
                    SELECT 1 FROM workspaces WHERE workspaces.name = t.ws_name
                );

                SELECT id INTO v_ws_emea    FROM workspaces WHERE name = 'Enterprise Sales EMEA'  LIMIT 1;
                SELECT id INTO v_ws_apac_gv FROM workspaces WHERE name = 'Enterprise Sales APAC'  LIMIT 1;
                SELECT id INTO v_ws_strat   FROM workspaces WHERE name = 'Strategic Accounts'     LIMIT 1;
                SELECT id INTO v_ws_inst    FROM workspaces WHERE name = 'Institutional Sales'    LIMIT 1;
                SELECT id INTO v_ws_retail  FROM workspaces WHERE name = 'Retail Investment'      LIMIT 1;
                SELECT id INTO v_ws_hosp    FROM workspaces WHERE name = 'Hospital Accounts'      LIMIT 1;
                SELECT id INTO v_ws_pharma  FROM workspaces WHERE name = 'Pharma Partnerships'    LIMIT 1;

                -- Workspace settings
                INSERT INTO workspace_settings (workspace_id, high_risk_threshold, medium_risk_threshold, risk_scoring_enabled, description)
                SELECT ws_id, hi, md, TRUE, desc_text
                FROM (VALUES
                    (v_ws_emea,    0.75::decimal, 0.45::decimal, 'EMEA enterprise deals pipeline'),
                    (v_ws_apac_gv, 0.70::decimal, 0.40::decimal, 'APAC regional enterprise sales'),
                    (v_ws_strat,   0.80::decimal, 0.50::decimal, 'Strategic key accounts management'),
                    (v_ws_inst,    0.65::decimal, 0.35::decimal, 'Institutional investment sales'),
                    (v_ws_retail,  0.60::decimal, 0.30::decimal, 'Retail investor acquisition'),
                    (v_ws_hosp,    0.72::decimal, 0.42::decimal, 'Hospital system accounts'),
                    (v_ws_pharma,  0.78::decimal, 0.48::decimal, 'Pharmaceutical partnerships pipeline')
                ) AS t(ws_id, hi, md, desc_text)
                WHERE t.ws_id IS NOT NULL
                ON CONFLICT (workspace_id) DO NOTHING;

                -- Workspace memberships
                -- EMEA: alice=admin, michael=mgr, laura=analyst, claire=member
                -- APAC GV: alice=admin, david=mgr, ryan=analyst
                -- Strategic: sarah=admin, alice=mgr, james=analyst, claire=member
                -- Institutional: james=admin, emma=mgr, oliver=analyst, claire=member
                -- Retail: james=admin, emma=mgr, ryan=member
                -- Hospital: sarah=admin, sophie=mgr, henry=analyst, claire=member
                -- Pharma: henry=admin, sarah=mgr, sophie=analyst, ryan=member
                INSERT INTO user_role_workspace (user_id, workspace_id, ws_role_id, joined_at, is_archived)
                SELECT u, ws, r, NOW() - INTERVAL '78 days', FALSE
                FROM (VALUES
                    (v_alice_id,   v_ws_emea,    v_ws_admin_role),
                    (v_michael_id, v_ws_emea,    v_ws_mgr_role),
                    (v_laura_id,   v_ws_emea,    v_ws_analyst_role),
                    (v_claire_id,  v_ws_emea,    v_ws_member_role),
                    (v_alice_id,   v_ws_apac_gv, v_ws_admin_role),
                    (v_david_id,   v_ws_apac_gv, v_ws_mgr_role),
                    (v_ryan_id,    v_ws_apac_gv, v_ws_analyst_role),
                    (v_sarah_id,   v_ws_strat,   v_ws_admin_role),
                    (v_alice_id,   v_ws_strat,   v_ws_mgr_role),
                    (v_james_id,   v_ws_strat,   v_ws_analyst_role),
                    (v_claire_id,  v_ws_strat,   v_ws_member_role),
                    (v_james_id,   v_ws_inst,    v_ws_admin_role),
                    (v_emma_id,    v_ws_inst,    v_ws_mgr_role),
                    (v_oliver_id,  v_ws_inst,    v_ws_analyst_role),
                    (v_claire_id,  v_ws_inst,    v_ws_member_role),
                    (v_james_id,   v_ws_retail,  v_ws_admin_role),
                    (v_emma_id,    v_ws_retail,  v_ws_mgr_role),
                    (v_ryan_id,    v_ws_retail,  v_ws_member_role),
                    (v_sarah_id,   v_ws_hosp,    v_ws_admin_role),
                    (v_sophie_id,  v_ws_hosp,    v_ws_mgr_role),
                    (v_henry_id,   v_ws_hosp,    v_ws_analyst_role),
                    (v_claire_id,  v_ws_hosp,    v_ws_member_role),
                    (v_henry_id,   v_ws_pharma,  v_ws_admin_role),
                    (v_sarah_id,   v_ws_pharma,  v_ws_mgr_role),
                    (v_sophie_id,  v_ws_pharma,  v_ws_analyst_role),
                    (v_ryan_id,    v_ws_pharma,  v_ws_member_role)
                ) AS t(u, ws, r)
                WHERE t.u IS NOT NULL AND t.ws IS NOT NULL AND t.r IS NOT NULL
                ON CONFLICT (user_id, workspace_id) DO NOTHING;

                -- ── Invitations and join requests ────────────────────────────────
                -- 5 pending invitations (future expiry)
                INSERT INTO organization_invitations (organization_id, email, org_role_id, invited_by_user_id, token, status, created_at, expires_at)
                VALUES
                    (v_org_gv, 'pending1@example.com', v_org_member_role, v_alice_id, gen_random_uuid()::text, 'Pending', NOW() - INTERVAL '1 day',  NOW() + INTERVAL '6 days'),
                    (v_org_gv, 'pending2@example.com', v_org_member_role, v_alice_id, gen_random_uuid()::text, 'Pending', NOW() - INTERVAL '2 days', NOW() + INTERVAL '5 days'),
                    (v_org_hc, 'pending3@example.com', v_org_member_role, v_sarah_id, gen_random_uuid()::text, 'Pending', NOW() - INTERVAL '1 day',  NOW() + INTERVAL '6 days'),
                    (v_org_fe, 'pending4@example.com', v_org_member_role, v_james_id, gen_random_uuid()::text, 'Pending', NOW() - INTERVAL '3 days', NOW() + INTERVAL '4 days'),
                    (v_org1,   'pending5@example.com', v_org_member_role, v_dorian_id,gen_random_uuid()::text, 'Pending', NOW() - INTERVAL '1 day',  NOW() + INTERVAL '6 days')
                ON CONFLICT DO NOTHING;

                -- 3 accepted invitations (historical)
                INSERT INTO organization_invitations (organization_id, email, org_role_id, invited_by_user_id, token, status, created_at, expires_at)
                VALUES
                    (v_org_gv, 'alice.morgan@relativa.com',  v_org_owner_role,  v_dorian_id, gen_random_uuid()::text, 'Accepted', NOW() - INTERVAL '82 days', NOW() - INTERVAL '75 days'),
                    (v_org_fe, 'james.carter@relativa.com',  v_org_owner_role,  v_dorian_id, gen_random_uuid()::text, 'Accepted', NOW() - INTERVAL '82 days', NOW() - INTERVAL '75 days'),
                    (v_org_hc, 'sarah.brooks@relativa.com',  v_org_owner_role,  v_dorian_id, gen_random_uuid()::text, 'Accepted', NOW() - INTERVAL '77 days', NOW() - INTERVAL '70 days')
                ON CONFLICT DO NOTHING;

                -- 4 pending join requests (2 Global Ventures, 2 HealthCore)
                -- 2 rejected join requests
                INSERT INTO organization_join_requests (user_id, organization_id, message, status, created_at)
                VALUES
                    (v_michael_id, v_org_hc, 'Interested in healthcare technology collaboration.', 'Pending',  NOW() - INTERVAL '5 days'),
                    (v_laura_id,   v_org_hc, 'Would like to explore pharma partnership opportunities.', 'Pending', NOW() - INTERVAL '3 days'),
                    (v_emma_id,    v_org_gv, 'Looking to expand into enterprise sales.', 'Pending',  NOW() - INTERVAL '2 days'),
                    (v_oliver_id,  v_org_gv, 'Keen to join the Global Ventures ecosystem.', 'Pending', NOW() - INTERVAL '1 day'),
                    (v_ryan_id,    v_org2,   'Interested in Tech Innovators community.', 'Rejected', NOW() - INTERVAL '20 days'),
                    (v_claire_id,  v_org2,   'Would love to collaborate with the tech team.', 'Rejected', NOW() - INTERVAL '15 days')
                ON CONFLICT DO NOTHING;

                -- Update rejected requests with reviewer info
                UPDATE organization_join_requests
                SET reviewed_by_user_id = v_dorian_id,
                    reviewed_at = created_at + INTERVAL '2 days'
                WHERE status = 'Rejected'
                  AND reviewed_by_user_id IS NULL
                  AND organization_id = v_org2;

            END $$;
            """
        );

        // ── Part B: Entity Seed (per workspace) ─────────────────────────────
        // Seeds 15 clients, 30 deals (+ deal_analysis + contract each), 15 contacts,
        // 20 tasks, and 10 notes into each of the 7 new workspaces.
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                -- Workspace IDs
                v_ws_ids        int[];
                v_ws_id         int;
                v_ws_count      int;
                v_creator_id    int;
                v_member_ids    int[];
                v_ws_idx        int;

                -- Entity type IDs
                v_client_type_id   int; v_deal_type_id     int;
                v_analysis_type_id int; v_contract_type_id int;
                v_contact_type_id  int; v_task_type_id     int;
                v_note_type_id     int;

                -- Relationship type IDs
                v_rel_deal_client_id    int; v_rel_deal_analysis_id int;
                v_rel_contract_deal_id  int; v_rel_client_contact_id int;
                v_rel_deal_contact_id   int; v_rel_deal_task_id      int;
                v_rel_client_task_id    int; v_rel_deal_note_id      int;
                v_rel_client_note_id    int;

                -- Property IDs
                v_prop_first_name     int; v_prop_last_name      int;
                v_prop_email          int; v_prop_country        int;
                v_prop_company_name   int; v_prop_industry       int;
                v_prop_website        int; v_prop_annual_revenue int;
                v_prop_employee_count int; v_prop_client_status  int;
                v_prop_client_ltv     int; v_prop_client_tenure  int;
                v_prop_title          int; v_prop_deal_value     int;
                v_prop_deal_stage     int; v_prop_status         int;
                v_prop_deal_source    int; v_prop_priority       int;
                v_prop_expected_close int; v_prop_created_at     int;
                v_prop_days_since_cr  int; v_prop_stage_enc      int;
                v_prop_num_interact   int; v_prop_days_since_l   int;
                v_prop_num_open       int; v_prop_avg_deal_val   int;
                v_prop_src_updated    int; v_prop_calc_at        int;
                v_prop_days_until_cl  int; v_prop_hist_close     int;
                v_prop_contract_num   int; v_prop_start_date     int;
                v_prop_end_date       int; v_prop_amount         int;
                v_prop_currency       int; v_prop_signed_at      int;
                v_prop_contract_status int; v_prop_contract_type int;
                v_prop_job_title      int; v_prop_department     int;
                v_prop_task_title     int; v_prop_task_status    int;
                v_prop_task_priority  int; v_prop_task_type      int;
                v_prop_due_date       int; v_prop_note_content   int;
                v_prop_note_date      int;

                -- Loop vars
                v_new_id       int; v_analysis_id int; v_contract_id int;
                v_client_ids   int[]; v_deal_ids int[];
                i              int; j int;

                -- Client data arrays (15 entries, cycled across workspaces)
                c_company text[] := ARRAY[
                    'Apex Solutions Group','Meridian Global Partners','Lighthouse Analytics',
                    'Pinnacle Financial Corp','Crestwood Technologies','Blue Harbor Industries',
                    'Summit Capital Advisors','Sterling Bridge LLC','Ironwood Consulting',
                    'Clearwater Systems','NovaStar Dynamics','Pacific Rim Ventures',
                    'Redstone Digital','Cobalt Innovations','Waverly Associates'];
                c_industry text[] := ARRAY[
                    'technology','finance','technology','finance','technology',
                    'manufacturing','finance','retail','technology','healthcare',
                    'technology','finance','retail','technology','finance'];
                c_status text[] := ARRAY[
                    'active','active','prospect','active','at_risk',
                    'active','active','lead','active','active',
                    'prospect','active','churned','active','active'];
                c_ltv decimal[] := ARRAY[
                    920000,1350000,480000,1750000,340000,
                    610000,980000,220000,760000,1100000,
                    550000,1420000,180000,830000,1650000];
                c_emp text[] := ARRAY[
                    '201-1000','1000+','51-200','1000+','51-200',
                    '201-1000','51-200','11-50','201-1000','1000+',
                    '51-200','1000+','1-10','201-1000','51-200'];
                c_revenue decimal[] := ARRAY[
                    4800000,12000000,2400000,18500000,1700000,
                    3200000,5100000,1100000,3800000,9600000,
                    2700000,14000000,900000,4200000,8800000];
                c_tenure int[] := ARRAY[
                    320,750,180,920,120,
                    410,640,80,280,560,
                    210,880,40,370,720];

                -- Deal data arrays (30 entries)
                d_title text[] := ARRAY[
                    'Global ERP Modernisation','AI-Driven Analytics Platform','Cloud Security Suite',
                    'Digital Transformation Program','Enterprise CRM Rollout','Supply Chain Intelligence',
                    'Predictive Risk Engine','Customer 360 Platform','Automated Compliance Suite',
                    'Smart Workflow Automation','Data Lake Infrastructure','API Gateway Deployment',
                    'Mobile Enterprise Platform','IoT Sensor Network','Cyber Threat Intelligence',
                    'Advanced BI Dashboard','Revenue Operations Suite','Partner Ecosystem Portal',
                    'Event-Driven Architecture','Managed DevOps Platform','Real-Time Fraud Detection',
                    'Multi-Cloud Strategy','Green IT Roadmap','Executive Insights Platform',
                    'Unified Communications Hub','Contract Lifecycle Manager','Talent Intelligence Suite',
                    'Product Intelligence Platform','Growth Marketing Engine','Operational Excellence Suite'];
                d_stage text[] := ARRAY[
                    'Prospecting','Prospecting','Prospecting','Prospecting','Prospecting',
                    'Prospecting','Qualification','Qualification','Qualification','Qualification',
                    'Qualification','Qualification','Proposal','Proposal','Proposal',
                    'Proposal','Proposal','Proposal','Negotiation','Negotiation',
                    'Negotiation','Negotiation','Negotiation','Negotiation',
                    NULL, NULL, NULL, NULL, NULL, NULL];
                d_status text[] := ARRAY[
                    'opened','opened','pending','opened','revoked',
                    'opened','opened','pending','closed','opened',
                    'pending','opened','opened','closed','opened',
                    'pending','revoked','closed','opened','pending',
                    'opened','closed','revoked','opened','closed',
                    'opened','pending','closed','revoked','opened'];
                d_source text[] := ARRAY[
                    'referral','cold_outreach','website','event','partner',
                    'referral','cold_outreach','website','event','partner',
                    'referral','cold_outreach','website','event','partner',
                    'referral','cold_outreach','website','event','partner',
                    'referral','cold_outreach','website','event','partner',
                    'referral','cold_outreach','website','event','partner'];
                d_priority text[] := ARRAY[
                    'high','high','medium','medium','low',
                    'high','high','medium','medium','high',
                    'low','medium','high','high','medium',
                    'medium','low','high','high','medium',
                    'high','low','medium','high','medium',
                    'high','medium','low','high','high'];
                d_value decimal[] := ARRAY[
                    285000,420000,175000,350000,65000,
                    510000,295000,190000,440000,155000,
                    380000,225000,490000,340000,130000,
                    270000,95000,415000,560000,210000,
                    325000,185000,75000,470000,240000,
                    310000,165000,520000,88000,395000];
                d_close_off int[] := ARRAY[
                    45,90,30,60,-10,
                    120,75,20,45,-30,
                    80,55,35,90,-20,
                    40,100,15,60,85,
                    25,-15,-45,70,50,
                    -60,110,30,-25,65];
                d_created_ago int[] := ARRAY[
                    20,25,35,40,180,
                    15,30,50,60,200,
                    22,45,18,28,75,
                    55,90,12,35,48,
                    65,110,160,25,42,
                    130,20,38,170,16];
                d_stage_enc int[] := ARRAY[
                    1,1,1,1,0,
                    1,2,2,2,2,
                    2,2,3,3,3,
                    3,3,3,4,4,
                    4,4,4,4,4,
                    0,0,0,0,0];
                d_interact int[] := ARRAY[
                    4,5,3,6,1,
                    8,7,5,9,3,
                    6,4,11,8,3,
                    7,2,14,17,9,
                    12,6,2,15,10,
                    3,18,13,1,20];
                d_hist_close decimal[] := ARRAY[
                    0.62,0.55,0.70,0.48,0.22,
                    0.78,0.65,0.60,0.82,0.38,
                    0.68,0.52,0.85,0.73,0.30,
                    0.60,0.25,0.88,0.91,0.75,
                    0.80,0.45,0.15,0.87,0.70,
                    0.20,0.93,0.77,0.10,0.95];

                -- Contact data (15 entries)
                con_first text[] := ARRAY[
                    'William','Charlotte','Alexander','Isabella','Benjamin',
                    'Amelia','Ethan','Harper','Matthew','Evelyn',
                    'Daniel','Abigail','Joseph','Emily','Christopher'];
                con_last  text[] := ARRAY[
                    'Parker','Hughes','Foster','Reed','Mitchell',
                    'Coleman','Rivera','Bell','Murphy','Watson',
                    'Simmons','Cox','Price','Ward','Griffin'];
                con_title text[] := ARRAY[
                    'CTO','VP Finance','COO','Head of Strategy','CFO',
                    'CEO','VP Sales','CIO','Head of Operations','Chief Data Officer',
                    'Director Engineering','VP Product','Head of Legal','Director Marketing','CISO'];
                con_dept  text[] := ARRAY[
                    'Technology','Finance','Operations','Strategy','Finance',
                    'Executive','Sales','IT','Operations','Analytics',
                    'Engineering','Product','Legal','Marketing','Security'];

                -- Task data (20 entries)
                t_title text[] := ARRAY[
                    'Initial discovery call','Send executive brief','Technical architecture review',
                    'Pricing negotiation session','Legal review of SOW','Stakeholder alignment meeting',
                    'Pilot environment setup','Executive sponsor introduction','ROI analysis presentation',
                    'Security assessment review','Reference customer call','Custom integration scoping',
                    'Contract redline review','Executive business case','Implementation planning',
                    'Quarterly business review','Competitive positioning demo','Risk assessment call',
                    'Onboarding plan review','Sign-off meeting'];
                t_status text[] := ARRAY[
                    'todo','todo','in_progress','in_progress','done',
                    'done','todo','todo','in_progress','done',
                    'in_progress','todo','done','in_progress','todo',
                    'done','in_progress','todo','done','in_progress'];
                t_prio text[] := ARRAY[
                    'high','high','high','medium','medium',
                    'low','high','medium','high','medium',
                    'high','high','medium','medium','low',
                    'medium','high','high','medium','low'];
                t_type text[] := ARRAY[
                    'call','email','meeting','meeting','meeting',
                    'meeting','call','meeting','meeting','meeting',
                    'call','meeting','meeting','meeting','meeting',
                    'meeting','demo','call','meeting','meeting'];
                t_due_off int[] := ARRAY[
                    -5,-2,3,7,14,
                    21,-1,5,10,30,
                    8,15,3,7,21,
                    60,12,18,5,2];

                -- Note data (10 entries)
                n_content text[] := ARRAY[
                    'Discovery call completed. Strong executive sponsorship confirmed. Moving to technical deep-dive next week.',
                    'CFO requested detailed 3-year ROI model before board approval. Finance team engagement needed.',
                    'Legal flagged indemnification clause. Redlined version sent back for review.',
                    'Pilot scope agreed: 3 departments, 90-day timeline. Success metrics defined.',
                    'Competitor pricing received. We win on integration depth and support SLA.',
                    'Executive sponsor confirmed budget allocation for Q3 start. Timeline acceleration possible.',
                    'Technical team assessment complete: 6-week implementation estimate, phased rollout recommended.',
                    'Reference call with existing customer positive. Client shared 40% efficiency improvement metric.',
                    'Security review passed with 2 minor findings. Remediation plan accepted by customer.',
                    'Contract signed. Kickoff scheduled for next Monday. Implementation team introduced.'];

            BEGIN
                -- ── Resolve entity types ─────────────────────────────────────────
                SELECT id INTO v_client_type_id   FROM entity_type WHERE name = 'client'       LIMIT 1;
                SELECT id INTO v_deal_type_id     FROM entity_type WHERE name = 'deal'         LIMIT 1;
                SELECT id INTO v_analysis_type_id FROM entity_type WHERE name = 'deal_analysis' LIMIT 1;
                SELECT id INTO v_contract_type_id FROM entity_type WHERE name = 'contract'     LIMIT 1;
                SELECT id INTO v_contact_type_id  FROM entity_type WHERE name = 'contact'      LIMIT 1;
                SELECT id INTO v_task_type_id     FROM entity_type WHERE name = 'task'         LIMIT 1;
                SELECT id INTO v_note_type_id     FROM entity_type WHERE name = 'note'         LIMIT 1;

                IF v_client_type_id IS NULL OR v_deal_type_id IS NULL THEN
                    RAISE EXCEPTION 'Required entity types not found — run earlier migrations first';
                END IF;

                -- ── Resolve relationship types ────────────────────────────────────
                SELECT id INTO v_rel_deal_client_id    FROM entity_relationship_type WHERE name = 'deal_client'    LIMIT 1;
                SELECT id INTO v_rel_deal_analysis_id  FROM entity_relationship_type WHERE name = 'deal_analysis'  LIMIT 1;
                SELECT id INTO v_rel_contract_deal_id  FROM entity_relationship_type WHERE name = 'contract_deal'  LIMIT 1;
                SELECT id INTO v_rel_client_contact_id FROM entity_relationship_type WHERE name = 'client_contact' LIMIT 1;
                SELECT id INTO v_rel_deal_contact_id   FROM entity_relationship_type WHERE name = 'deal_contact'   LIMIT 1;
                SELECT id INTO v_rel_deal_task_id      FROM entity_relationship_type WHERE name = 'deal_task'      LIMIT 1;
                SELECT id INTO v_rel_client_task_id    FROM entity_relationship_type WHERE name = 'client_task'    LIMIT 1;
                SELECT id INTO v_rel_deal_note_id      FROM entity_relationship_type WHERE name = 'deal_note'      LIMIT 1;
                SELECT id INTO v_rel_client_note_id    FROM entity_relationship_type WHERE name = 'client_note'    LIMIT 1;

                -- ── Resolve properties ───────────────────────────────────────────
                SELECT id INTO v_prop_first_name      FROM property WHERE name = 'first_name'              AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_last_name       FROM property WHERE name = 'last_name'               AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_email           FROM property WHERE name = 'email'                   AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_country         FROM property WHERE name = 'country'                 AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_company_name    FROM property WHERE name = 'company_name'            AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_industry        FROM property WHERE name = 'industry'                AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_website         FROM property WHERE name = 'website'                 AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_annual_revenue  FROM property WHERE name = 'annual_revenue'          AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_employee_count  FROM property WHERE name = 'employee_count'          AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_client_status   FROM property WHERE name = 'client_status'           AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_client_ltv      FROM property WHERE name = 'client_lifetime_value'   AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_client_tenure   FROM property WHERE name = 'client_tenure_days'      AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_title           FROM property WHERE name = 'title'                   AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_deal_value      FROM property WHERE name = 'deal_value'              AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_deal_stage      FROM property WHERE name = 'deal_stage'              AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_status          FROM property WHERE name = 'status'                  AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_deal_source     FROM property WHERE name = 'deal_source'             AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_priority        FROM property WHERE name = 'priority'                AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_expected_close  FROM property WHERE name = 'expected_close'          AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_created_at      FROM property WHERE name = 'created_at'              AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_days_since_cr   FROM property WHERE name = 'days_since_created'       AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_stage_enc       FROM property WHERE name = 'stage_encoded'            AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_num_interact    FROM property WHERE name = 'num_interactions'         AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_days_since_l    FROM property WHERE name = 'days_since_last_contact'  AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_num_open        FROM property WHERE name = 'num_open_deals'           AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_avg_deal_val    FROM property WHERE name = 'avg_deal_value'           AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_src_updated     FROM property WHERE name = 'source_updated_at'        AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_calc_at         FROM property WHERE name = 'calculated_at'            AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_days_until_cl   FROM property WHERE name = 'days_until_expected_close' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_hist_close      FROM property WHERE name = 'historical_close_rate'    AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_contract_num    FROM property WHERE name = 'contract_number'          AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_start_date      FROM property WHERE name = 'start_date'               AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_end_date        FROM property WHERE name = 'end_date'                 AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_amount          FROM property WHERE name = 'amount'                   AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_currency        FROM property WHERE name = 'currency'                 AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_signed_at       FROM property WHERE name = 'signed_at'                AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_contract_status FROM property WHERE name = 'contract_status'          AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_contract_type   FROM property WHERE name = 'contract_type'            AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_job_title       FROM property WHERE name = 'job_title'                AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_department      FROM property WHERE name = 'department'               AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_task_title      FROM property WHERE name = 'task_title'               AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_task_status     FROM property WHERE name = 'task_status'              AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_task_priority   FROM property WHERE name = 'task_priority'            AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_task_type       FROM property WHERE name = 'task_type'                AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_due_date        FROM property WHERE name = 'due_date'                 AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_note_content    FROM property WHERE name = 'note_content'             AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_note_date       FROM property WHERE name = 'note_date'                AND organization_id IS NULL LIMIT 1;

                -- ── Collect new workspace IDs ─────────────────────────────────────
                SELECT ARRAY_AGG(id ORDER BY id) INTO v_ws_ids
                FROM workspaces
                WHERE name IN (
                    'Enterprise Sales EMEA','Enterprise Sales APAC','Strategic Accounts',
                    'Institutional Sales','Retail Investment',
                    'Hospital Accounts','Pharma Partnerships'
                );

                IF v_ws_ids IS NULL OR array_length(v_ws_ids, 1) = 0 THEN
                    RAISE NOTICE 'New workspaces not found — skipping entity seed';
                    RETURN;
                END IF;

                v_ws_count := array_length(v_ws_ids, 1);

                -- ── Loop: seed each workspace ────────────────────────────────────
                FOR v_ws_idx IN 1..v_ws_count LOOP
                    v_ws_id := v_ws_ids[v_ws_idx];
                    SELECT created_by_user_id INTO v_creator_id FROM workspaces WHERE id = v_ws_id;

                    v_client_ids := ARRAY[]::int[];
                    v_deal_ids   := ARRAY[]::int[];

                    -- ── 15 clients ───────────────────────────────────────────────
                    FOR i IN 1..15 LOOP
                        INSERT INTO entity (entity_type_id, created_by_user_id, is_archived)
                        VALUES (v_client_type_id, v_creator_id,
                                -- Archive ~10% (every 10th)
                                CASE WHEN i % 10 = 0 THEN TRUE ELSE FALSE END)
                        RETURNING id INTO v_new_id;

                        INSERT INTO entity_workspace (entity_id, workspace_id) VALUES (v_new_id, v_ws_id);

                        INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                        SELECT v_new_id, prop, val_s, val_i, val_d, NULL, NULL
                        FROM (
                            SELECT v_prop_company_name   AS prop,
                                   c_company[1 + ((i + v_ws_idx * 3) % 15)]  AS val_s, NULL::int AS val_i, NULL::decimal AS val_d
                            UNION ALL SELECT v_prop_industry,      c_industry[1 + ((i + v_ws_idx) % 15)],     NULL, NULL
                            UNION ALL SELECT v_prop_client_status, c_status[1 + ((i + v_ws_idx * 2) % 15)],   NULL, NULL
                            UNION ALL SELECT v_prop_employee_count,c_emp[1 + ((i - 1) % 15)],                 NULL, NULL
                            UNION ALL SELECT v_prop_website,
                                   LOWER(REGEXP_REPLACE(c_company[1 + ((i + v_ws_idx * 3) % 15)], '[^a-zA-Z0-9]', '', 'g')) || '.com',
                                   NULL, NULL
                            UNION ALL SELECT v_prop_email,
                                   'info@' || LOWER(REGEXP_REPLACE(c_company[1 + ((i + v_ws_idx * 3) % 15)], '[^a-zA-Z0-9]', '', 'g')) || '.com',
                                   NULL, NULL
                            UNION ALL SELECT v_prop_country, CASE (i % 5)
                                   WHEN 0 THEN 'United States'
                                   WHEN 1 THEN 'United Kingdom'
                                   WHEN 2 THEN 'Germany'
                                   WHEN 3 THEN 'Canada'
                                   ELSE 'Australia' END, NULL, NULL
                            UNION ALL SELECT v_prop_annual_revenue, NULL, NULL, c_revenue[1 + ((i - 1) % 15)]
                            UNION ALL SELECT v_prop_client_ltv,     NULL, NULL, c_ltv[1 + ((i - 1) % 15)]
                            UNION ALL SELECT v_prop_client_tenure,  NULL, c_tenure[1 + ((i - 1) % 15)], NULL
                        ) AS vals(prop, val_s, val_i, val_d)
                        WHERE prop IS NOT NULL;

                        v_client_ids := v_client_ids || v_new_id;
                    END LOOP;

                    -- ── 30 deals ──────────────────────────────────────────────────
                    FOR i IN 1..30 LOOP
                        INSERT INTO entity (entity_type_id, created_by_user_id, is_archived)
                        VALUES (v_deal_type_id, v_creator_id,
                                CASE WHEN i % 10 = 0 THEN TRUE ELSE FALSE END)
                        RETURNING id INTO v_new_id;

                        INSERT INTO entity_workspace (entity_id, workspace_id) VALUES (v_new_id, v_ws_id);

                        -- Core deal properties
                        INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                        SELECT v_new_id, prop, val_s, NULL, val_d, NULL, val_date
                        FROM (
                            SELECT v_prop_title        AS prop, d_title[1 + ((i + v_ws_idx * 5) % 30)]   AS val_s, NULL::decimal AS val_d, NULL::date AS val_date
                            UNION ALL SELECT v_prop_status,      d_status[i],              NULL, NULL
                            UNION ALL SELECT v_prop_deal_source, d_source[1 + ((i - 1) % 30)], NULL, NULL
                            UNION ALL SELECT v_prop_priority,    d_priority[1 + ((i - 1) % 30)], NULL, NULL
                            UNION ALL SELECT v_prop_deal_value,  NULL,         d_value[1 + ((i - 1) % 30)], NULL
                            UNION ALL SELECT v_prop_expected_close, NULL,      NULL, CURRENT_DATE + d_close_off[1 + ((i - 1) % 30)]
                            UNION ALL SELECT v_prop_created_at,  NULL,         NULL, CURRENT_DATE - d_created_ago[1 + ((i - 1) % 30)]
                        ) AS vals(prop, val_s, val_d, val_date)
                        WHERE prop IS NOT NULL;

                        -- Deal stage (only for deals 1-24 — 80% have stage)
                        IF i <= 24 AND d_stage[i] IS NOT NULL AND v_prop_deal_stage IS NOT NULL THEN
                            INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                            VALUES (v_new_id, v_prop_deal_stage, d_stage[i], NULL, NULL, NULL, NULL)
                            ON CONFLICT DO NOTHING;
                        END IF;

                        -- Link deal → client (many_to_one)
                        IF v_rel_deal_client_id IS NOT NULL AND array_length(v_client_ids, 1) > 0 THEN
                            INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                            VALUES (v_new_id, v_client_ids[1 + ((i - 1) % array_length(v_client_ids, 1))], v_rel_deal_client_id);
                        END IF;

                        v_deal_ids := v_deal_ids || v_new_id;

                        -- ── deal_analysis (one per deal) ─────────────────────────
                        IF v_analysis_type_id IS NOT NULL AND v_rel_deal_analysis_id IS NOT NULL THEN
                            INSERT INTO entity (entity_type_id, created_by_user_id, is_archived)
                            VALUES (v_analysis_type_id, v_creator_id, FALSE)
                            RETURNING id INTO v_analysis_id;

                            INSERT INTO entity_workspace (entity_id, workspace_id) VALUES (v_analysis_id, v_ws_id);

                            INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                            VALUES (v_new_id, v_analysis_id, v_rel_deal_analysis_id);

                            INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                            SELECT v_analysis_id, prop, NULL, val_i, val_d, NULL, val_date
                            FROM (
                                SELECT v_prop_days_since_cr AS prop, d_created_ago[1 + ((i - 1) % 30)] AS val_i, NULL::decimal AS val_d, NULL::date AS val_date
                                UNION ALL SELECT v_prop_stage_enc,    d_stage_enc[1 + ((i - 1) % 30)],   NULL, NULL
                                UNION ALL SELECT v_prop_num_interact, d_interact[1 + ((i - 1) % 30)],    NULL, NULL
                                UNION ALL SELECT v_prop_days_since_l, 2 + (i % 15), NULL, NULL
                                UNION ALL SELECT v_prop_num_open,     1 + (i % 4),  NULL, NULL
                                UNION ALL SELECT v_prop_avg_deal_val, NULL, d_value[1 + ((i - 1) % 30)], NULL
                                UNION ALL SELECT v_prop_hist_close,   NULL, d_hist_close[1 + ((i - 1) % 30)], NULL
                                UNION ALL SELECT v_prop_src_updated,  NULL, NULL, CURRENT_DATE
                                UNION ALL SELECT v_prop_calc_at,      NULL, NULL, CURRENT_DATE
                                UNION ALL SELECT v_prop_days_until_cl, GREATEST(0, d_close_off[1 + ((i - 1) % 30)]), NULL, NULL
                            ) AS vals(prop, val_i, val_d, val_date)
                            WHERE prop IS NOT NULL;
                        END IF;

                        -- ── contract (one per deal, required by contract_deal) ────
                        IF v_contract_type_id IS NOT NULL AND v_rel_contract_deal_id IS NOT NULL THEN
                            INSERT INTO entity (entity_type_id, created_by_user_id, is_archived)
                            VALUES (v_contract_type_id, v_creator_id, FALSE)
                            RETURNING id INTO v_contract_id;

                            INSERT INTO entity_workspace (entity_id, workspace_id) VALUES (v_contract_id, v_ws_id);

                            -- contract_deal: contract (source) → deal (target), is_required=TRUE
                            INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                            VALUES (v_contract_id, v_new_id, v_rel_contract_deal_id);

                            INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                            SELECT v_contract_id, prop, val_s, NULL, val_d, NULL, val_date
                            FROM (
                                SELECT v_prop_contract_num    AS prop,
                                       'CN-' || v_ws_idx::text || '-' || i::text AS val_s, NULL::decimal AS val_d, NULL::date AS val_date
                                UNION ALL SELECT v_prop_amount,         NULL, d_value[1 + ((i - 1) % 30)], NULL
                                UNION ALL SELECT v_prop_currency,
                                    CASE (i % 3) WHEN 0 THEN 'EUR' WHEN 1 THEN 'USD' ELSE 'GBP' END, NULL, NULL
                                UNION ALL SELECT v_prop_contract_status,
                                    CASE d_status[i] WHEN 'revoked' THEN 'revoked' ELSE 'active' END, NULL, NULL
                                UNION ALL SELECT v_prop_contract_type,
                                    CASE (i % 3) WHEN 0 THEN 'subscription' WHEN 1 THEN 'one_time' ELSE 'retainer' END, NULL, NULL
                                UNION ALL SELECT v_prop_start_date, NULL, NULL, CURRENT_DATE - d_created_ago[1 + ((i - 1) % 30)]
                                UNION ALL SELECT v_prop_end_date,   NULL, NULL, CURRENT_DATE + 365
                                UNION ALL SELECT v_prop_signed_at,  NULL, NULL,
                                    CASE WHEN d_status[i] IN ('closed', 'revoked')
                                         THEN CURRENT_DATE - GREATEST(0, d_created_ago[1 + ((i - 1) % 30)] - 7)
                                         ELSE CURRENT_DATE - 5 END
                            ) AS vals(prop, val_s, val_d, val_date)
                            WHERE prop IS NOT NULL;
                        END IF;
                    END LOOP; -- end 30 deals

                    -- ── 15 contacts ───────────────────────────────────────────────
                    FOR i IN 1..15 LOOP
                        IF v_contact_type_id IS NULL THEN EXIT; END IF;

                        INSERT INTO entity (entity_type_id, created_by_user_id, is_archived)
                        VALUES (v_contact_type_id, v_creator_id, FALSE)
                        RETURNING id INTO v_new_id;

                        INSERT INTO entity_workspace (entity_id, workspace_id) VALUES (v_new_id, v_ws_id);

                        INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                        SELECT v_new_id, prop, val_s, NULL, NULL, NULL, NULL
                        FROM (
                            SELECT v_prop_first_name AS prop, con_first[1 + ((i - 1) % 15)] AS val_s
                            UNION ALL SELECT v_prop_last_name,  con_last[1 + ((i + v_ws_idx) % 15)]
                            UNION ALL SELECT v_prop_email,
                                LOWER(con_first[1 + ((i - 1) % 15)]) || '.' || LOWER(con_last[1 + ((i + v_ws_idx) % 15)]) || '@client' || i::text || '.com'
                            UNION ALL SELECT v_prop_job_title,  con_title[1 + ((i - 1) % 15)]
                            UNION ALL SELECT v_prop_department, con_dept[1 + ((i - 1) % 15)]
                            UNION ALL SELECT v_prop_country, CASE (i % 5)
                                WHEN 0 THEN 'United States'
                                WHEN 1 THEN 'United Kingdom'
                                WHEN 2 THEN 'Germany'
                                WHEN 3 THEN 'Canada'
                                ELSE 'Australia' END
                        ) AS vals(prop, val_s)
                        WHERE prop IS NOT NULL;

                        -- Link client → contact
                        IF v_rel_client_contact_id IS NOT NULL AND array_length(v_client_ids, 1) > 0 THEN
                            INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                            VALUES (v_client_ids[1 + ((i - 1) % array_length(v_client_ids, 1))], v_new_id, v_rel_client_contact_id);
                        END IF;

                        -- Link deal → contact (first 15 deals)
                        IF v_rel_deal_contact_id IS NOT NULL
                           AND i <= array_length(v_deal_ids, 1) THEN
                            INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                            VALUES (v_deal_ids[i], v_new_id, v_rel_deal_contact_id);
                        END IF;
                    END LOOP;

                    -- ── 20 tasks ──────────────────────────────────────────────────
                    FOR i IN 1..20 LOOP
                        IF v_task_type_id IS NULL THEN EXIT; END IF;

                        INSERT INTO entity (entity_type_id, created_by_user_id, is_archived)
                        VALUES (v_task_type_id, v_creator_id, FALSE)
                        RETURNING id INTO v_new_id;

                        INSERT INTO entity_workspace (entity_id, workspace_id) VALUES (v_new_id, v_ws_id);

                        INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                        SELECT v_new_id, prop, val_s, NULL, NULL, NULL, val_date
                        FROM (
                            SELECT v_prop_task_title    AS prop, t_title[1 + ((i + v_ws_idx * 3) % 20)]  AS val_s, NULL::date AS val_date
                            UNION ALL SELECT v_prop_task_status,   t_status[1 + ((i - 1) % 20)],         NULL
                            UNION ALL SELECT v_prop_task_priority, t_prio[1 + ((i - 1) % 20)],           NULL
                            UNION ALL SELECT v_prop_task_type,     t_type[1 + ((i - 1) % 20)],           NULL
                            UNION ALL SELECT v_prop_due_date,      NULL,                                  CURRENT_DATE + t_due_off[1 + ((i - 1) % 20)]
                        ) AS vals(prop, val_s, val_date)
                        WHERE prop IS NOT NULL;

                        -- Link deal → task
                        IF v_rel_deal_task_id IS NOT NULL
                           AND i <= array_length(v_deal_ids, 1) THEN
                            INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                            VALUES (v_deal_ids[i], v_new_id, v_rel_deal_task_id);
                        END IF;

                        -- Link client → task for tasks 11-20
                        IF i > 10 AND v_rel_client_task_id IS NOT NULL AND array_length(v_client_ids, 1) > 0 THEN
                            INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                            VALUES (v_client_ids[1 + ((i - 11) % array_length(v_client_ids, 1))], v_new_id, v_rel_client_task_id);
                        END IF;
                    END LOOP;

                    -- ── 10 notes ──────────────────────────────────────────────────
                    FOR i IN 1..10 LOOP
                        IF v_note_type_id IS NULL THEN EXIT; END IF;

                        INSERT INTO entity (entity_type_id, created_by_user_id, is_archived)
                        VALUES (v_note_type_id, v_creator_id, FALSE)
                        RETURNING id INTO v_new_id;

                        INSERT INTO entity_workspace (entity_id, workspace_id) VALUES (v_new_id, v_ws_id);

                        INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                        SELECT v_new_id, prop, val_s, NULL, NULL, NULL, val_date
                        FROM (
                            SELECT v_prop_note_content AS prop, n_content[1 + ((i + v_ws_idx) % 10)] AS val_s, NULL::date AS val_date
                            UNION ALL SELECT v_prop_note_date, NULL, CURRENT_DATE - (i * 4)
                        ) AS vals(prop, val_s, val_date)
                        WHERE prop IS NOT NULL;

                        -- Link deal → note (note is non-standalone, MUST have parent)
                        IF v_rel_deal_note_id IS NOT NULL
                           AND i <= array_length(v_deal_ids, 1) THEN
                            INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                            VALUES (v_deal_ids[i], v_new_id, v_rel_deal_note_id);
                        ELSIF v_rel_client_note_id IS NOT NULL AND array_length(v_client_ids, 1) > 0 THEN
                            -- Fallback: link to client if no deal available
                            INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                            VALUES (v_client_ids[1 + ((i - 1) % array_length(v_client_ids, 1))], v_new_id, v_rel_client_note_id);
                        END IF;
                    END LOOP;

                END LOOP; -- end workspace loop
            END $$;
            """
        );

        // ── Part C: Audit Log Seed ────────────────────────────────────────────
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                v_user_ids   int[];
                v_org_ids    int[];
                v_ws_ids     int[];
                v_entity_ids int[];
                v_ws_entity  int[];
                v_u          int; v_o int; v_w int; v_e int;
                i            int;
                v_days_ago   int;
                v_actions_entity    text[] := ARRAY['entity_created','entity_updated','entity_archived','entity_created','entity_updated'];
                v_actions_org       text[] := ARRAY['organization_created','organization_member_added','organization_member_removed','organization_settings_updated','organization_role_created','organization_invitation_created','organization_invitation_accepted','organization_member_role_changed'];
                v_actions_ws        text[] := ARRAY['workspace_created','workspace_member_added','workspace_member_removed','workspace_settings_updated','workspace_member_role_changed','workspace_updated','workspace_archived','workspace_member_added'];
                v_actions_user      text[] := ARRAY['user_registered','user_profile_updated','user_password_reset_requested','user_password_reset_completed','user_profile_updated'];
                v_entity_types_str  text[] := ARRAY['client','deal','contact','task','note','deal_analysis','contract'];
                v_fields            text[] := ARRAY['status','title','deal_stage','priority','company_name','task_status','note_content','client_status','deal_value','expected_close'];
                v_old_vals          text[] := ARRAY['"opened"','"Prospecting"','"high"','"lead"','"todo"','"active"','{"dealValue": 200000}','{"status": "opened"}','{"stage": "Prospecting"}','{"priority": "low"}'];
                v_new_vals          text[] := ARRAY['"pending"','"Qualification"','"medium"','"active"','"in_progress"','"at_risk"','{"dealValue": 350000}','{"status": "closed"}','{"stage": "Proposal"}','{"priority": "high"}'];
            BEGIN
                -- Collect IDs for realistic references (use subqueries so ORDER BY + LIMIT work correctly with ARRAY_AGG)
                SELECT ARRAY_AGG(id) INTO v_user_ids   FROM (SELECT id FROM users          WHERE is_archived = FALSE ORDER BY id LIMIT 15)  u;
                SELECT ARRAY_AGG(id) INTO v_org_ids    FROM (SELECT id FROM organizations  WHERE is_archived = FALSE ORDER BY id)            o;
                SELECT ARRAY_AGG(id) INTO v_ws_ids     FROM (SELECT id FROM workspaces     WHERE is_archived = FALSE ORDER BY id LIMIT 10)   w;
                SELECT ARRAY_AGG(id) INTO v_entity_ids FROM (SELECT id FROM entity         WHERE is_archived = FALSE ORDER BY id LIMIT 100)  e;

                IF v_user_ids IS NULL THEN RETURN; END IF;

                -- ── entity_audit_log: 200 rows ───────────────────────────────────
                FOR i IN 1..200 LOOP
                    v_days_ago := (i % 90);
                    v_u := v_user_ids[1 + ((i - 1) % array_length(v_user_ids, 1))];
                    v_e := CASE WHEN v_entity_ids IS NOT NULL
                                THEN v_entity_ids[1 + ((i - 1) % array_length(v_entity_ids, 1))]
                                ELSE NULL END;

                    INSERT INTO entity_audit_log (
                        id, action, changed_by, entity_id, entity_type,
                        field_name, old_value, new_value, changed_at
                    ) VALUES (
                        gen_random_uuid(),
                        v_actions_entity[1 + ((i - 1) % array_length(v_actions_entity, 1))],
                        v_u,
                        v_e,
                        v_entity_types_str[1 + ((i - 1) % array_length(v_entity_types_str, 1))],
                        CASE WHEN v_actions_entity[1 + ((i - 1) % array_length(v_actions_entity, 1))] = 'entity_updated'
                             THEN v_fields[1 + ((i - 1) % array_length(v_fields, 1))]
                             ELSE NULL END,
                        CASE WHEN v_actions_entity[1 + ((i - 1) % array_length(v_actions_entity, 1))] = 'entity_updated'
                             THEN v_old_vals[1 + ((i - 1) % array_length(v_old_vals, 1))]::jsonb
                             ELSE NULL END,
                        CASE WHEN v_actions_entity[1 + ((i - 1) % array_length(v_actions_entity, 1))] IN ('entity_updated', 'entity_created')
                             THEN v_new_vals[1 + ((i - 1) % array_length(v_new_vals, 1))]::jsonb
                             ELSE '{"isArchived": true}'::jsonb END,
                        NOW() - (v_days_ago || ' days')::interval - ((i % 24) || ' hours')::interval
                    );
                END LOOP;

                -- ── organization_audit_log: 80 rows ──────────────────────────────
                FOR i IN 1..80 LOOP
                    v_days_ago := (i % 85);
                    v_u := v_user_ids[1 + ((i - 1) % array_length(v_user_ids, 1))];
                    v_o := CASE WHEN v_org_ids IS NOT NULL
                                THEN v_org_ids[1 + ((i - 1) % array_length(v_org_ids, 1))]
                                ELSE NULL END;

                    INSERT INTO organization_audit_log (
                        id, action, changed_by, organization_id,
                        field_name, old_value, new_value, changed_at
                    ) VALUES (
                        gen_random_uuid(),
                        v_actions_org[1 + ((i - 1) % array_length(v_actions_org, 1))],
                        v_u,
                        v_o,
                        CASE WHEN v_actions_org[1 + ((i - 1) % array_length(v_actions_org, 1))] = 'organization_settings_updated'
                             THEN 'join_policy' ELSE NULL END,
                        CASE WHEN v_actions_org[1 + ((i - 1) % array_length(v_actions_org, 1))] = 'organization_settings_updated'
                             THEN '"open"'::jsonb ELSE NULL END,
                        CASE WHEN v_actions_org[1 + ((i - 1) % array_length(v_actions_org, 1))] = 'organization_settings_updated'
                             THEN '"invite_only"'::jsonb
                             ELSE ('{"userId": ' || (v_u)::text || '}')::jsonb END,
                        NOW() - (v_days_ago || ' days')::interval - ((i % 24) || ' hours')::interval
                    );
                END LOOP;

                -- ── workspace_audit_log: 100 rows ────────────────────────────────
                FOR i IN 1..100 LOOP
                    v_days_ago := (i % 88);
                    v_u := v_user_ids[1 + ((i - 1) % array_length(v_user_ids, 1))];
                    v_w := CASE WHEN v_ws_ids IS NOT NULL
                                THEN v_ws_ids[1 + ((i - 1) % array_length(v_ws_ids, 1))]
                                ELSE NULL END;

                    INSERT INTO workspace_audit_log (
                        id, action, changed_by, workspace_id,
                        field_name, old_value, new_value, changed_at
                    ) VALUES (
                        gen_random_uuid(),
                        v_actions_ws[1 + ((i - 1) % array_length(v_actions_ws, 1))],
                        v_u,
                        v_w,
                        CASE WHEN v_actions_ws[1 + ((i - 1) % array_length(v_actions_ws, 1))] IN ('workspace_settings_updated', 'workspace_updated')
                             THEN 'name' ELSE NULL END,
                        CASE WHEN v_actions_ws[1 + ((i - 1) % array_length(v_actions_ws, 1))] IN ('workspace_settings_updated', 'workspace_updated')
                             THEN '"Old Workspace Name"'::jsonb ELSE NULL END,
                        CASE WHEN v_actions_ws[1 + ((i - 1) % array_length(v_actions_ws, 1))] IN ('workspace_settings_updated', 'workspace_updated')
                             THEN '"Updated Workspace Name"'::jsonb
                             ELSE ('{"userId": ' || v_u::text || ', "workspaceId": ' || COALESCE(v_w::text, 'null') || '}')::jsonb END,
                        NOW() - (v_days_ago || ' days')::interval - ((i % 24) || ' hours')::interval
                    );
                END LOOP;

                -- ── user_audit_log: 80 rows ──────────────────────────────────────
                FOR i IN 1..80 LOOP
                    v_days_ago := (i % 87);
                    v_u := v_user_ids[1 + ((i - 1) % array_length(v_user_ids, 1))];
                    -- actor and target can differ
                    DECLARE
                        v_actor int := v_user_ids[1 + (i % array_length(v_user_ids, 1))];
                        v_target int := v_u;
                    BEGIN
                        INSERT INTO user_audit_log (
                            id, action, changed_by, target_user_id,
                            field_name, old_value, new_value, changed_at
                        ) VALUES (
                            gen_random_uuid(),
                            v_actions_user[1 + ((i - 1) % array_length(v_actions_user, 1))],
                            v_actor,
                            v_target,
                            CASE WHEN v_actions_user[1 + ((i - 1) % array_length(v_actions_user, 1))] = 'user_profile_updated'
                                 THEN CASE (i % 3) WHEN 0 THEN 'first_name' WHEN 1 THEN 'last_name' ELSE 'email' END
                                 ELSE NULL END,
                            CASE WHEN v_actions_user[1 + ((i - 1) % array_length(v_actions_user, 1))] = 'user_profile_updated'
                                 THEN '"Old Value"'::jsonb ELSE NULL END,
                            CASE WHEN v_actions_user[1 + ((i - 1) % array_length(v_actions_user, 1))] = 'user_profile_updated'
                                 THEN '"New Value"'::jsonb
                                 ELSE ('{"userId": ' || v_target::text || '}')::jsonb END,
                            NOW() - (v_days_ago || ' days')::interval - ((i % 24) || ' hours')::interval
                        );
                    END;
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
                v_new_user_emails text[] := ARRAY[
                    'alice.morgan@relativa.com','james.carter@relativa.com','sarah.brooks@relativa.com',
                    'michael.hayes@relativa.com','laura.scott@relativa.com','david.chen@relativa.com',
                    'emma.wilson@relativa.com','oliver.grant@relativa.com','sophie.lane@relativa.com',
                    'henry.ford@relativa.com','claire.west@relativa.com','ryan.bishop@relativa.com'
                ];
                v_new_ws_names text[] := ARRAY[
                    'Enterprise Sales EMEA','Enterprise Sales APAC','Strategic Accounts',
                    'Institutional Sales','Retail Investment','Hospital Accounts','Pharma Partnerships'
                ];
                v_new_org_names text[] := ARRAY['Global Ventures Ltd','FinEdge Capital','HealthCore Systems'];
                v_user_ids   int[]; v_ws_ids int[]; v_org_ids int[];
                v_entity_ids int[];
            BEGIN
                SELECT ARRAY_AGG(id) INTO v_user_ids FROM users WHERE email = ANY(v_new_user_emails);
                SELECT ARRAY_AGG(id) INTO v_ws_ids   FROM workspaces WHERE name = ANY(v_new_ws_names);
                SELECT ARRAY_AGG(id) INTO v_org_ids  FROM organizations WHERE name = ANY(v_new_org_names);

                -- Remove entities in new workspaces
                IF v_ws_ids IS NOT NULL THEN
                    SELECT ARRAY_AGG(DISTINCT entity_id) INTO v_entity_ids
                    FROM entity_workspace WHERE workspace_id = ANY(v_ws_ids);

                    IF v_entity_ids IS NOT NULL THEN
                        DELETE FROM entity_relationship WHERE source_entity_id = ANY(v_entity_ids) OR target_entity_id = ANY(v_entity_ids);
                        DELETE FROM entity_property_value WHERE entity_id = ANY(v_entity_ids);
                        DELETE FROM entity_workspace WHERE entity_id = ANY(v_entity_ids);
                        DELETE FROM entity WHERE id = ANY(v_entity_ids);
                    END IF;

                    DELETE FROM user_role_workspace WHERE workspace_id = ANY(v_ws_ids);
                    DELETE FROM workspace_settings WHERE workspace_id = ANY(v_ws_ids);
                    DELETE FROM workspaces WHERE id = ANY(v_ws_ids);
                END IF;

                -- Remove org data
                IF v_org_ids IS NOT NULL THEN
                    DELETE FROM organization_join_requests WHERE organization_id = ANY(v_org_ids);
                    DELETE FROM organization_invitations WHERE organization_id = ANY(v_org_ids);
                    DELETE FROM user_role_organization WHERE organization_id = ANY(v_org_ids);
                    DELETE FROM organization_settings WHERE organization_id = ANY(v_org_ids);
                    DELETE FROM organizations WHERE id = ANY(v_org_ids);
                END IF;

                -- Remove new users and their org memberships
                IF v_user_ids IS NOT NULL THEN
                    DELETE FROM user_role_organization WHERE user_id = ANY(v_user_ids);
                    DELETE FROM users WHERE id = ANY(v_user_ids);
                END IF;

                -- Remove audit log entries added by this migration
                -- (approximate: rows added in the last 24h that look like seed rows)
                DELETE FROM entity_audit_log       WHERE changed_at > NOW() - INTERVAL '1 day' AND id IN (SELECT id FROM entity_audit_log ORDER BY changed_at DESC LIMIT 200);
                DELETE FROM organization_audit_log WHERE changed_at > NOW() - INTERVAL '1 day' AND id IN (SELECT id FROM organization_audit_log ORDER BY changed_at DESC LIMIT 80);
                DELETE FROM workspace_audit_log    WHERE changed_at > NOW() - INTERVAL '1 day' AND id IN (SELECT id FROM workspace_audit_log ORDER BY changed_at DESC LIMIT 100);
                DELETE FROM user_audit_log         WHERE changed_at > NOW() - INTERVAL '1 day' AND id IN (SELECT id FROM user_audit_log ORDER BY changed_at DESC LIMIT 80);
            END $$;
            """
        );
    }
}
