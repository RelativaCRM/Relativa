using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

/// <summary>
/// Seeds 20 clients, 50 deals (with deal_analysis + contracts), 30 contacts, 40 tasks,
/// and 25 notes with realistic CRM data for dashboard demonstration.
/// Idempotency guard: skips if 40+ non-archived deal entities already exist.
/// </summary>
[DbContext(typeof(MigrationDbContext))]
[Migration("20260517110000_SeedProductionCrmData")]
public partial class SeedProductionCrmData : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                v_deal_type_id     int;
                v_client_type_id   int;
                v_contact_type_id  int;
                v_task_type_id     int;
                v_note_type_id     int;
                v_analysis_type_id int;
                v_contract_type_id int;

                v_rel_deal_client_id    int;
                v_rel_deal_analysis_id  int;
                v_rel_deal_contract_id  int;
                v_rel_client_contact_id int;
                v_rel_deal_contact_id   int;
                v_rel_deal_task_id      int;
                v_rel_client_task_id    int;
                v_rel_deal_note_id      int;

                v_prop_first_name     int;
                v_prop_last_name      int;
                v_prop_email          int;
                v_prop_country        int;
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

                v_prop_days_since_created int;
                v_prop_stage_encoded      int;
                v_prop_num_interactions   int;
                v_prop_days_since_last    int;
                v_prop_num_open_deals     int;
                v_prop_avg_deal_value     int;
                v_prop_source_updated_at  int;
                v_prop_calculated_at      int;
                v_prop_days_until_close   int;
                v_prop_hist_close_rate    int;

                v_prop_contract_number int;
                v_prop_start_date      int;
                v_prop_end_date        int;
                v_prop_amount          int;
                v_prop_currency        int;
                v_prop_signed_at       int;
                v_prop_contract_status int;
                v_prop_contract_type   int;

                v_prop_job_title   int;
                v_prop_department  int;

                v_prop_task_title    int;
                v_prop_task_status   int;
                v_prop_task_priority int;
                v_prop_task_type     int;
                v_prop_due_date      int;

                v_prop_note_content int;
                v_prop_note_date    int;

                v_workspace_id  int;
                v_ws_ids        int[];
                v_ws_count      int;
                v_creator_id    int;
                v_client_ids    int[] := ARRAY[]::int[];
                v_deal_ids      int[] := ARRAY[]::int[];
                v_new_id        int;
                v_analysis_id   int;
                v_contract_id   int;
                i               int;

                -- client data (20)
                c_company   text[]    := ARRAY['TechVision Inc','FinCore Capital','MedHealth Solutions','RetailFlow Corp','ManufactEdge Ltd','GreenEnergy Systems','EduLearn Platform','DataStream Analytics','SecureVault Finance','PharmaTech Global','LogiChain Retail','IndustrialCore Mfg','SolarPeak Energy','SkillBridge Edu','CloudNative Tech','BrightWave Finance','CareLink Health','ShopZone Retail','PowerGrid Energy','NextGen Systems'];
                c_industry  text[]    := ARRAY['technology','finance','healthcare','retail','manufacturing','energy','education','technology','finance','healthcare','retail','manufacturing','energy','education','technology','finance','healthcare','retail','manufacturing','technology'];
                c_cstatus   text[]    := ARRAY['active','active','active','at_risk','active','prospect','active','active','churned','active','active','lead','active','prospect','active','at_risk','active','churned','active','active'];
                c_ltv       decimal[] := ARRAY[850000,1200000,620000,380000,540000,180000,290000,760000,90000,1100000,420000,50000,340000,120000,930000,210000,480000,70000,660000,1800000];
                c_emp       text[]    := ARRAY['201-1000','51-200','51-200','201-1000','51-200','11-50','11-50','51-200','11-50','1000+','51-200','1-10','51-200','11-50','201-1000','11-50','51-200','1-10','201-1000','1000+'];
                c_website   text[]    := ARRAY['techvision.io','fincore.com','medhealth.net','retailflow.com','manufacedge.co','greenenergy.io','edulearn.app','datastream.ai','securevault.fi','pharmatech.global','logichain.com','industrcore.net','solarpeak.energy','skillbridge.edu','cloudnative.io','brightwave.fi','carelink.health','shopzone.retail','powergrid.co','nextgen.systems'];
                c_email     text[]    := ARRAY['info@techvision.io','contact@fincore.com','hello@medhealth.net','info@retailflow.com','info@manufacedge.co','info@greenenergy.io','team@edulearn.app','hello@datastream.ai','secure@securevault.fi','info@pharmatech.global','info@logichain.com','admin@industrcore.net','info@solarpeak.energy','team@skillbridge.edu','info@cloudnative.io','info@brightwave.fi','care@carelink.health','info@shopzone.retail','ops@powergrid.co','info@nextgen.systems'];
                c_annual    decimal[] := ARRAY[4200000,8500000,3100000,1800000,2600000,900000,1400000,3800000,450000,9200000,2100000,250000,1700000,600000,5100000,1050000,2400000,350000,3300000,12000000];

                -- deal data (50)
                d_title text[] := ARRAY[
                    'Enterprise SaaS License','Cloud Platform Migration','Data Analytics Suite',
                    'Security Infrastructure Upgrade','API Integration Package',
                    'Treasury Management System','Risk Analytics Platform','Regulatory Compliance Suite',
                    'Investment Portfolio Tool','Payment Gateway Integration',
                    'EMR System Modernization','Patient Analytics Platform','Clinical Trial Management',
                    'Medical Device Connectivity','HIPAA Compliance Audit',
                    'Supply Chain Optimization','Inventory Management System','Customer Loyalty Platform',
                    'Point-of-Sale Upgrade','Omnichannel Commerce Platform',
                    'Predictive Maintenance Suite','Quality Control Automation','Production Planning System',
                    'Industrial IoT Platform','Shop Floor Analytics',
                    'Smart Grid Management','Renewable Energy Dashboard','Carbon Tracking Platform',
                    'Energy Audit System','Grid Optimization Suite',
                    'Learning Management System','Student Analytics Platform','Content Delivery Network',
                    'Assessment Automation Tool','Curriculum Design Suite',
                    'DataOps Platform','MLOps Infrastructure','Real-Time Analytics Engine',
                    'Data Governance Suite','BI Dashboard Package',
                    'Wealth Management Portal','Portfolio Rebalancing Tool','Compliance Monitoring Suite',
                    'Insurance Analytics Platform','Digital Banking Platform',
                    'Supply Chain Visibility','Retail Analytics Suite','Manufacturing Analytics',
                    'Energy Management Platform','Enterprise AI Platform'];

                d_client_idx int[] := ARRAY[
                    1,1,1,1,1,
                    2,2,2,2,2,
                    3,3,3,3,3,
                    4,4,4,4,4,
                    5,5,5,5,5,
                    6,6,7,7,8,
                    8,8,9,9,10,
                    10,10,11,11,12,
                    12,13,13,14,15,
                    15,16,17,18,20];

                d_stage text[] := ARRAY[
                    'Prospecting','Prospecting','Prospecting','Prospecting','Prospecting',
                    'Prospecting','Prospecting','Prospecting','Prospecting','Prospecting',
                    'Prospecting','Prospecting','Prospecting','Prospecting','Prospecting',
                    'Qualification','Qualification','Qualification','Qualification','Qualification',
                    'Qualification','Qualification','Qualification','Qualification','Qualification',
                    'Qualification','Qualification','Proposal','Proposal','Proposal',
                    'Proposal','Proposal','Proposal','Proposal','Proposal',
                    'Proposal','Proposal','Negotiation','Negotiation','Negotiation',
                    'Negotiation','Negotiation','Negotiation','Negotiation','Negotiation',
                    NULL,NULL,NULL,NULL,NULL];

                d_status text[] := ARRAY[
                    'opened','opened','opened','pending','revoked',
                    'opened','opened','pending','pending','revoked',
                    'opened','closed','closed','closed','revoked',
                    'opened','opened','pending','pending','closed',
                    'opened','opened','closed','revoked','closed',
                    'pending','pending','opened','opened','pending',
                    'closed','closed','opened','revoked','closed',
                    'closed','closed','opened','opened','pending',
                    'pending','closed','closed','revoked','closed',
                    'closed','revoked','closed','revoked','opened'];

                d_source text[] := ARRAY[
                    'referral','website','cold_outreach','event','partner',
                    'cold_outreach','referral','website','event','partner',
                    'referral','cold_outreach','website','event','partner',
                    'referral','website','cold_outreach','event','partner',
                    'cold_outreach','referral','partner','website','event',
                    'referral','cold_outreach','website','partner','event',
                    'cold_outreach','website','referral','event','partner',
                    'website','partner','referral','cold_outreach','event',
                    'referral','partner','website','cold_outreach','event',
                    'cold_outreach','referral','website','event','partner'];

                d_priority text[] := ARRAY[
                    'high','high','medium','medium','low',
                    'high','medium','medium','high','low',
                    'high','high','medium','low','medium',
                    'high','medium','low','high','medium',
                    'high','high','medium','low','medium',
                    'medium','low','high','high','medium',
                    'high','medium','medium','low','high',
                    'high','medium','high','medium','low',
                    'medium','high','high','low','medium',
                    'high','medium','low','medium','high'];

                d_value decimal[] := ARRAY[
                    245000,180000,95000,320000,42000,
                    480000,280000,195000,380000,88000,
                    215000,145000,320000,78000,55000,
                    165000,92000,265000,110000,340000,
                    175000,390000,240000,38000,185000,
                    280000,95000,410000,320000,155000,
                    225000,140000,75000,25000,310000,
                    460000,285000,390000,270000,120000,
                    345000,480000,215000,32000,295000,
                    175000,65000,440000,18000,520000];

                -- days from today for expected_close (negative = past)
                d_close_offset int[] := ARRAY[
                    45,90,120,30,-20,
                    75,100,60,30,-40,
                    80,-10,-30,-60,-90,
                    50,85,40,25,-15,
                    70,95,-5,-80,35,
                    60,110,55,90,20,
                    -20,-45,65,100,-10,
                    -30,-60,45,75,35,
                    20,-25,-15,-70,30,
                    -40,-90,-25,-110,120];

                d_created_ago int[] := ARRAY[
                    30,35,40,45,180,
                    25,30,45,55,190,
                    28,65,80,120,200,
                    20,25,40,50,35,
                    22,27,50,170,32,
                    40,45,18,22,35,
                    60,85,24,28,42,
                    55,90,16,20,38,
                    45,12,18,140,25,
                    70,160,15,175,8];

                d_stage_enc int[] := ARRAY[
                    1,1,1,1,0,
                    1,1,1,1,0,
                    1,1,1,0,0,
                    2,2,2,2,2,
                    2,2,2,0,2,
                    2,2,3,3,3,
                    3,3,3,0,3,
                    3,3,4,4,4,
                    4,4,4,0,4,
                    4,0,4,0,4];

                d_interactions int[] := ARRAY[
                    5,4,6,3,1,
                    8,7,5,4,2,
                    6,12,10,3,2,
                    9,7,6,5,15,
                    8,11,14,1,9,
                    7,5,18,16,8,
                    11,9,7,2,13,
                    17,12,20,15,6,
                    10,22,19,2,11,
                    8,2,25,1,14];

                d_hist_close decimal[] := ARRAY[
                    0.65,0.58,0.72,0.45,0.20,
                    0.80,0.75,0.68,0.55,0.30,
                    0.70,0.78,0.82,0.35,0.25,
                    0.62,0.55,0.60,0.50,0.88,
                    0.67,0.75,0.85,0.15,0.72,
                    0.70,0.58,0.88,0.82,0.65,
                    0.78,0.72,0.55,0.20,0.80,
                    0.90,0.83,0.92,0.87,0.60,
                    0.75,0.95,0.89,0.10,0.77,
                    0.68,0.22,0.93,0.08,0.85];

                -- contact data (30)
                ct_first  text[] := ARRAY['James','Sarah','Michael','Emma','David','Lisa','Robert','Anna','Thomas','Julia','Chris','Maria','Andrew','Sophie','Kevin','Laura','Daniel','Helen','Mark','Rachel','Paul','Claire','Peter','Grace','John','Alice','Eric','Diana','Frank','Amy'];
                ct_last   text[] := ARRAY['Wilson','Chen','Rodriguez','Thompson','Kim','Patel','Johnson','Muller','Brown','Santos','Davis','Novak','Taylor','Williams','Lee','Martin','Anderson','Garcia','Harris','Clark','Lewis','Turner','Walker','Evans','Hall','Baker','Scott','Green','Adams','Nelson'];
                ct_title  text[] := ARRAY['CTO','VP Sales','COO','CFO','IT Director','Head of Strategy','CEO','Head of Finance','VP Engineering','Chief of Staff','Head of Operations','Director BD','VP Product','Head of Legal','CIO','VP Marketing','Head of HR','CFO','Director IT','VP Sales','COO','CTO','Head of Procurement','VP Finance','CEO','Director Operations','CTO','VP Engineering','Head of Analytics','Chief Strategy Officer'];
                ct_dept   text[] := ARRAY['Technology','Sales','Operations','Finance','IT','Strategy','Executive','Finance','Engineering','Executive','Operations','Business Dev','Product','Legal','IT','Marketing','HR','Finance','IT','Sales','Operations','Technology','Procurement','Finance','Executive','Operations','Technology','Engineering','Analytics','Strategy'];
                ct_client  int[] := ARRAY[1,1,2,2,3,3,4,5,5,6,7,8,8,9,10,10,11,12,13,14,15,15,16,17,18,19,19,20,1,3];
                ct_email  text[] := ARRAY['j.wilson@techvision.io','s.chen@techvision.io','m.rodriguez@fincore.com','e.thompson@fincore.com','d.kim@medhealth.net','l.patel@medhealth.net','r.johnson@retailflow.com','a.muller@manufacedge.co','t.brown@manufacedge.co','j.santos@greenenergy.io','c.davis@edulearn.app','m.novak@datastream.ai','a.taylor@datastream.ai','s.williams@securevault.fi','k.lee@pharmatech.global','l.martin@pharmatech.global','d.anderson@logichain.com','h.garcia@industrcore.net','m.harris@solarpeak.energy','r.clark@skillbridge.edu','p.lewis@cloudnative.io','c.turner@cloudnative.io','pe.walker@brightwave.fi','g.evans@carelink.health','j.hall@shopzone.retail','a.baker@powergrid.co','e.scott@powergrid.co','d.green@nextgen.systems','f.adams@techvision.io','amy.nelson@medhealth.net'];

                -- task data (40)
                t_title   text[] := ARRAY['Schedule discovery call','Send product demo','Follow up on proposal','Prepare contract draft','Review legal terms','Update CRM records','Send pricing breakdown','Book technical demo','Conduct needs assessment','Send case studies','Prepare executive presentation','Review SOW','Arrange reference calls','Send trial credentials','Onboarding kickoff call','Quarterly business review','Negotiate payment terms','Finalize integration scope','Security review meeting','Sign NDA','Send RFP response','Arrange site visit','Product roadmap walkthrough','Budget approval meeting','Contract renewal discussion','Competitive analysis review','Stakeholder alignment call','Technical architecture review','Compliance documentation','Pilot program setup','Performance review meeting','Upsell opportunity meeting','Risk assessment call','Project timeline review','Support escalation follow-up','Partnership agreement review','Custom integration planning','Training session scheduling','Success metrics alignment','Executive sponsor meeting'];
                t_status  text[] := ARRAY['todo','todo','in_progress','in_progress','done','done','todo','todo','in_progress','done','in_progress','todo','done','in_progress','done','todo','in_progress','todo','done','in_progress','todo','done','in_progress','todo','done','in_progress','todo','done','in_progress','todo','done','in_progress','todo','done','in_progress','todo','done','in_progress','todo','done'];
                t_prio    text[] := ARRAY['high','high','high','medium','medium','low','high','medium','high','medium','high','high','medium','medium','low','medium','high','high','medium','low','high','medium','high','medium','low','medium','high','high','medium','medium','low','high','medium','high','low','medium','high','medium','high','low'];
                t_type    text[] := ARRAY['call','demo','email','email','call','email','call','demo','call','email','meeting','meeting','call','email','call','meeting','call','meeting','meeting','meeting','email','meeting','demo','meeting','call','meeting','call','meeting','meeting','call','meeting','meeting','call','meeting','call','meeting','call','meeting','meeting','meeting'];
                t_due_off int[]  := ARRAY[-5,-2,3,7,14,21,-1,5,10,30,8,15,3,7,21,60,12,18,5,2,8,20,4,15,45,10,6,12,3,25,90,7,4,20,2,30,10,15,8,60];
                t_deal_idx int[] := ARRAY[1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32,33,34,35,36,37,38,39,40];

                -- note data (25)
                n_content text[] := ARRAY[
                    'Initial discovery call went well. Client interested in enterprise tier. Requesting custom pricing.',
                    'Demo completed successfully. Technical team impressed with API capabilities. Next step: pilot proposal.',
                    'CFO requested detailed ROI analysis before proceeding. Scheduling follow-up with finance team.',
                    'Legal raised concerns about data residency requirements. Working with compliance team to address.',
                    'Client confirmed budget approved for Q3. Moving to contract negotiation phase.',
                    'Reference call arranged with existing customer. Positive outcome expected.',
                    'Competitor pricing submitted. Need to differentiate on support and integration capabilities.',
                    'Technical deep-dive completed. 3-month implementation timeline agreed.',
                    'Executive sponsor meeting postponed. Rescheduling for next week.',
                    'Trial extended by 2 weeks at client request. Good engagement metrics observed.',
                    'Procurement team requesting additional security documentation. Expediting internal review.',
                    'Stakeholder alignment meeting successful. All departments signed off on scope.',
                    'Payment terms negotiated: net-60 with 3-installment schedule.',
                    'Implementation team introduction call completed. Client team assigned.',
                    'Upsell opportunity identified: client interested in advanced analytics module.',
                    'Competitive analysis complete. Our solution outperforms on 7 of 9 criteria.',
                    'Client contact changed. New point of contact is VP Product — updating relationships.',
                    'Partnership opportunity discussed. Client may become a channel partner.',
                    'Security audit passed with no critical findings.',
                    'Pilot results reviewed: 23% efficiency improvement measured.',
                    'Annual contract renewal confirmed. Expanding to 2 additional departments.',
                    'Q4 business review: NPS score 8.5. Strong expansion potential identified.',
                    'Integration scope finalized. Custom connectors needed for legacy ERP system.',
                    'Risk assessment completed. Medium risk profile — manageable with proper SLAs.',
                    'Training program designed. 3-day on-site sessions planned for January.'];
                n_date_off int[] := ARRAY[-2,-5,-8,-12,-15,-18,-3,-7,-10,-14,-1,-4,-6,-9,-11,-13,-2,-5,-8,-11,-1,-3,-6,-9,-12];
                n_deal_idx int[] := ARRAY[1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25];

            BEGIN
                -- ── Guard ────────────────────────────────────────────────────────
                SELECT id INTO v_deal_type_id FROM entity_type WHERE name = 'deal' LIMIT 1;
                IF v_deal_type_id IS NULL THEN RETURN; END IF;
                IF (SELECT COUNT(*) FROM entity WHERE entity_type_id = v_deal_type_id AND is_archived = FALSE) >= 40 THEN
                    RETURN;
                END IF;

                -- ── Entity type IDs ──────────────────────────────────────────────
                SELECT id INTO v_client_type_id   FROM entity_type WHERE name = 'client'       LIMIT 1;
                SELECT id INTO v_contact_type_id  FROM entity_type WHERE name = 'contact'      LIMIT 1;
                SELECT id INTO v_task_type_id     FROM entity_type WHERE name = 'task'         LIMIT 1;
                SELECT id INTO v_note_type_id     FROM entity_type WHERE name = 'note'         LIMIT 1;
                SELECT id INTO v_analysis_type_id FROM entity_type WHERE name = 'deal_analysis' LIMIT 1;
                SELECT id INTO v_contract_type_id FROM entity_type WHERE name = 'contract'     LIMIT 1;

                -- ── Relationship type IDs ────────────────────────────────────────
                SELECT id INTO v_rel_deal_client_id    FROM entity_relationship_type WHERE name = 'deal_client'    LIMIT 1;
                SELECT id INTO v_rel_deal_analysis_id  FROM entity_relationship_type WHERE name = 'deal_analysis'  LIMIT 1;
                SELECT id INTO v_rel_deal_contract_id  FROM entity_relationship_type WHERE name = 'deal_contract'  LIMIT 1;
                SELECT id INTO v_rel_client_contact_id FROM entity_relationship_type WHERE name = 'client_contact' LIMIT 1;
                SELECT id INTO v_rel_deal_contact_id   FROM entity_relationship_type WHERE name = 'deal_contact'   LIMIT 1;
                SELECT id INTO v_rel_deal_task_id      FROM entity_relationship_type WHERE name = 'deal_task'      LIMIT 1;
                SELECT id INTO v_rel_client_task_id    FROM entity_relationship_type WHERE name = 'client_task'    LIMIT 1;
                SELECT id INTO v_rel_deal_note_id      FROM entity_relationship_type WHERE name = 'deal_note'      LIMIT 1;

                -- ── Property IDs ─────────────────────────────────────────────────
                SELECT id INTO v_prop_first_name     FROM property WHERE name = 'first_name'              AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_last_name      FROM property WHERE name = 'last_name'               AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_email          FROM property WHERE name = 'email'                   AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_country        FROM property WHERE name = 'country'                 AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_company_name   FROM property WHERE name = 'company_name'            AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_industry       FROM property WHERE name = 'industry'                AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_website        FROM property WHERE name = 'website'                 AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_annual_revenue FROM property WHERE name = 'annual_revenue'          AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_employee_count FROM property WHERE name = 'employee_count'          AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_client_status  FROM property WHERE name = 'client_status'           AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_client_ltv     FROM property WHERE name = 'client_lifetime_value'   AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_client_tenure  FROM property WHERE name = 'client_tenure_days'      AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_title          FROM property WHERE name = 'title'                   AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_deal_value     FROM property WHERE name = 'deal_value'              AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_deal_stage     FROM property WHERE name = 'deal_stage'              AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_status         FROM property WHERE name = 'status'                  AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_deal_source    FROM property WHERE name = 'deal_source'             AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_priority       FROM property WHERE name = 'priority'                AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_expected_close FROM property WHERE name = 'expected_close'          AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_created_at     FROM property WHERE name = 'created_at'              AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_days_since_created FROM property WHERE name = 'days_since_created'       AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_stage_encoded      FROM property WHERE name = 'stage_encoded'            AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_num_interactions   FROM property WHERE name = 'num_interactions'         AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_days_since_last    FROM property WHERE name = 'days_since_last_contact'  AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_num_open_deals     FROM property WHERE name = 'num_open_deals'           AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_avg_deal_value     FROM property WHERE name = 'avg_deal_value'           AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_source_updated_at  FROM property WHERE name = 'source_updated_at'        AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_calculated_at      FROM property WHERE name = 'calculated_at'            AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_days_until_close   FROM property WHERE name = 'days_until_expected_close' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_hist_close_rate    FROM property WHERE name = 'historical_close_rate'    AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_contract_number FROM property WHERE name = 'contract_number' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_start_date      FROM property WHERE name = 'start_date'      AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_end_date        FROM property WHERE name = 'end_date'        AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_amount          FROM property WHERE name = 'amount'          AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_currency        FROM property WHERE name = 'currency'        AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_signed_at       FROM property WHERE name = 'signed_at'       AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_contract_status FROM property WHERE name = 'contract_status' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_contract_type   FROM property WHERE name = 'contract_type'   AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_job_title   FROM property WHERE name = 'job_title'   AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_department  FROM property WHERE name = 'department'  AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_task_title    FROM property WHERE name = 'task_title'    AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_task_status   FROM property WHERE name = 'task_status'   AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_task_priority FROM property WHERE name = 'task_priority' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_task_type     FROM property WHERE name = 'task_type'     AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_due_date      FROM property WHERE name = 'due_date'      AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_note_content FROM property WHERE name = 'note_content' AND organization_id IS NULL LIMIT 1;
                SELECT id INTO v_prop_note_date    FROM property WHERE name = 'note_date'    AND organization_id IS NULL LIMIT 1;

                -- ── Workspace setup ──────────────────────────────────────────────
                SELECT ARRAY_AGG(id ORDER BY id) INTO v_ws_ids
                FROM workspaces WHERE is_archived = FALSE;
                IF v_ws_ids IS NULL OR array_length(v_ws_ids, 1) = 0 THEN RETURN; END IF;
                v_ws_count     := array_length(v_ws_ids, 1);
                v_workspace_id := v_ws_ids[1];
                SELECT created_by_user_id INTO v_creator_id FROM workspaces WHERE id = v_workspace_id;

                -- ── 20 clients ───────────────────────────────────────────────────
                FOR i IN 1..20 LOOP
                    INSERT INTO entity (entity_type_id, is_archived, created_by_user_id)
                    VALUES (v_client_type_id, FALSE, v_creator_id)
                    RETURNING id INTO v_new_id;

                    INSERT INTO entity_workspace (entity_id, workspace_id)
                    VALUES (v_new_id, v_ws_ids[1 + ((i - 1) % v_ws_count)])
                    ON CONFLICT DO NOTHING;

                    INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                    SELECT v_new_id, prop, val_s, val_i, val_d, NULL, NULL
                    FROM (
                        SELECT v_prop_first_name      AS prop, c_company[i]   AS val_s, NULL::int AS val_i, NULL::decimal AS val_d
                        UNION ALL SELECT v_prop_company_name,  c_company[i],   NULL, NULL
                        UNION ALL SELECT v_prop_industry,      c_industry[i],  NULL, NULL
                        UNION ALL SELECT v_prop_client_status, c_cstatus[i],   NULL, NULL
                        UNION ALL SELECT v_prop_employee_count,c_emp[i],       NULL, NULL
                        UNION ALL SELECT v_prop_website,       c_website[i],   NULL, NULL
                        UNION ALL SELECT v_prop_email,         c_email[i],     NULL, NULL
                        UNION ALL SELECT v_prop_country,       'United States', NULL, NULL
                        UNION ALL SELECT v_prop_annual_revenue,NULL,            NULL, c_annual[i]
                        UNION ALL SELECT v_prop_client_ltv,    NULL,            NULL, c_ltv[i]
                        UNION ALL SELECT v_prop_client_tenure, NULL,            180 + i * 15, NULL
                    ) AS vals(prop, val_s, val_i, val_d)
                    WHERE prop IS NOT NULL;

                    v_client_ids := v_client_ids || v_new_id;
                END LOOP;

                -- ── 50 deals ─────────────────────────────────────────────────────
                FOR i IN 1..50 LOOP
                    INSERT INTO entity (entity_type_id, is_archived, created_by_user_id)
                    VALUES (v_deal_type_id, FALSE, v_creator_id)
                    RETURNING id INTO v_new_id;

                    INSERT INTO entity_workspace (entity_id, workspace_id)
                    VALUES (v_new_id, v_ws_ids[1 + ((i - 1) % v_ws_count)])
                    ON CONFLICT DO NOTHING;

                    -- Core string/decimal/date properties
                    INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                    SELECT v_new_id, prop, val_s, NULL, val_d, NULL, val_date
                    FROM (
                        SELECT v_prop_title       AS prop, d_title[i]    AS val_s, NULL::decimal AS val_d, NULL::date AS val_date
                        UNION ALL SELECT v_prop_status,      d_status[i],   NULL, NULL
                        UNION ALL SELECT v_prop_deal_source, d_source[i],   NULL, NULL
                        UNION ALL SELECT v_prop_priority,    d_priority[i], NULL, NULL
                        UNION ALL SELECT v_prop_deal_value,  NULL,          d_value[i], NULL
                        UNION ALL SELECT v_prop_expected_close, NULL,       NULL, CURRENT_DATE + d_close_offset[i]
                        UNION ALL SELECT v_prop_created_at,  NULL,          NULL, CURRENT_DATE - d_created_ago[i]
                    ) AS vals(prop, val_s, val_d, val_date)
                    WHERE prop IS NOT NULL;

                    -- deal_stage only for deals 1-45 (non-null stage)
                    IF d_stage[i] IS NOT NULL AND v_prop_deal_stage IS NOT NULL THEN
                        INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                        VALUES (v_new_id, v_prop_deal_stage, d_stage[i], NULL, NULL, NULL, NULL)
                        ON CONFLICT DO NOTHING;
                    END IF;

                    -- Link deal → client
                    IF v_rel_deal_client_id IS NOT NULL
                       AND d_client_idx[i] >= 1 AND d_client_idx[i] <= array_length(v_client_ids, 1) THEN
                        INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                        VALUES (v_new_id, v_client_ids[d_client_idx[i]], v_rel_deal_client_id);
                    END IF;

                    v_deal_ids := v_deal_ids || v_new_id;
                END LOOP;

                -- ── deal_analysis (one per deal) ──────────────────────────────────
                FOR i IN 1..array_length(v_deal_ids, 1) LOOP
                    IF v_analysis_type_id IS NULL OR v_rel_deal_analysis_id IS NULL THEN EXIT; END IF;

                    INSERT INTO entity (entity_type_id, is_archived, created_by_user_id)
                    VALUES (v_analysis_type_id, FALSE, v_creator_id)
                    RETURNING id INTO v_analysis_id;

                    INSERT INTO entity_workspace (entity_id, workspace_id)
                    SELECT v_analysis_id, workspace_id FROM entity_workspace WHERE entity_id = v_deal_ids[i]
                    ON CONFLICT DO NOTHING;

                    INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                    VALUES (v_deal_ids[i], v_analysis_id, v_rel_deal_analysis_id);

                    INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                    SELECT v_analysis_id, prop, NULL, val_i, val_d, NULL, val_date
                    FROM (
                        SELECT v_prop_days_since_created AS prop, d_created_ago[i]    AS val_i, NULL::decimal AS val_d, NULL::date AS val_date
                        UNION ALL SELECT v_prop_stage_encoded,    d_stage_enc[i],      NULL, NULL
                        UNION ALL SELECT v_prop_num_interactions, d_interactions[i],   NULL, NULL
                        UNION ALL SELECT v_prop_days_since_last,
                            CASE d_status[i] WHEN 'opened' THEN 3 + (i % 10)
                                             WHEN 'pending' THEN 1 + (i % 5)
                                             WHEN 'closed'  THEN 0
                                             ELSE 90 + (i % 30) END,
                            NULL, NULL
                        UNION ALL SELECT v_prop_num_open_deals, 1 + (i % 4), NULL, NULL
                        UNION ALL SELECT v_prop_avg_deal_value, NULL, d_value[i], NULL
                        UNION ALL SELECT v_prop_hist_close_rate, NULL, d_hist_close[i], NULL
                        UNION ALL SELECT v_prop_source_updated_at, NULL, NULL, CURRENT_DATE
                        UNION ALL SELECT v_prop_calculated_at,     NULL, NULL, CURRENT_DATE
                        UNION ALL SELECT v_prop_days_until_close,  GREATEST(0, d_close_offset[i]), NULL, NULL
                    ) AS vals(prop, val_i, val_d, val_date)
                    WHERE prop IS NOT NULL;
                END LOOP;

                -- ── contracts (one per deal) ──────────────────────────────────────
                FOR i IN 1..array_length(v_deal_ids, 1) LOOP
                    IF v_contract_type_id IS NULL OR v_rel_deal_contract_id IS NULL THEN EXIT; END IF;

                    INSERT INTO entity (entity_type_id, is_archived, created_by_user_id)
                    VALUES (v_contract_type_id, FALSE, v_creator_id)
                    RETURNING id INTO v_contract_id;

                    INSERT INTO entity_workspace (entity_id, workspace_id)
                    SELECT v_contract_id, workspace_id FROM entity_workspace WHERE entity_id = v_deal_ids[i]
                    ON CONFLICT DO NOTHING;

                    INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                    VALUES (v_deal_ids[i], v_contract_id, v_rel_deal_contract_id);

                    INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                    SELECT v_contract_id, prop, val_s, NULL, val_d, NULL, val_date
                    FROM (
                        SELECT v_prop_contract_number AS prop, 'CN-' || i::text AS val_s, NULL::decimal AS val_d, NULL::date AS val_date
                        UNION ALL SELECT v_prop_amount, NULL, d_value[i], NULL
                        UNION ALL SELECT v_prop_currency,
                            CASE i % 3 WHEN 0 THEN 'EUR' WHEN 1 THEN 'USD' ELSE 'GBP' END, NULL, NULL
                        UNION ALL SELECT v_prop_contract_status,
                            CASE d_status[i] WHEN 'revoked' THEN 'revoked' ELSE 'active' END, NULL, NULL
                        UNION ALL SELECT v_prop_contract_type,
                            CASE i % 3 WHEN 0 THEN 'subscription' WHEN 1 THEN 'one_time' ELSE 'retainer' END, NULL, NULL
                        UNION ALL SELECT v_prop_start_date,  NULL, NULL, CURRENT_DATE - d_created_ago[i]
                        UNION ALL SELECT v_prop_end_date,    NULL, NULL, CURRENT_DATE + 365
                        UNION ALL SELECT v_prop_signed_at,   NULL, NULL,
                            CASE WHEN d_status[i] IN ('closed','revoked')
                                 THEN CURRENT_DATE - GREATEST(0, d_created_ago[i] - 7)
                                 ELSE NULL END
                    ) AS vals(prop, val_s, val_d, val_date)
                    WHERE prop IS NOT NULL;
                END LOOP;

                -- ── 30 contacts ───────────────────────────────────────────────────
                FOR i IN 1..30 LOOP
                    IF v_contact_type_id IS NULL THEN EXIT; END IF;

                    INSERT INTO entity (entity_type_id, is_archived, created_by_user_id)
                    VALUES (v_contact_type_id, FALSE, v_creator_id)
                    RETURNING id INTO v_new_id;

                    INSERT INTO entity_workspace (entity_id, workspace_id)
                    VALUES (v_new_id, v_ws_ids[1 + ((i - 1) % v_ws_count)])
                    ON CONFLICT DO NOTHING;

                    INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                    SELECT v_new_id, prop, val_s, NULL, NULL, NULL, NULL
                    FROM (
                        SELECT v_prop_first_name AS prop, ct_first[i]  AS val_s
                        UNION ALL SELECT v_prop_last_name,  ct_last[i]
                        UNION ALL SELECT v_prop_email,      ct_email[i]
                        UNION ALL SELECT v_prop_job_title,  ct_title[i]
                        UNION ALL SELECT v_prop_department, ct_dept[i]
                        UNION ALL SELECT v_prop_country,   'United States'
                    ) AS vals(prop, val_s)
                    WHERE prop IS NOT NULL;

                    -- client → contact
                    IF v_rel_client_contact_id IS NOT NULL
                       AND ct_client[i] >= 1 AND ct_client[i] <= array_length(v_client_ids, 1) THEN
                        INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                        VALUES (v_client_ids[ct_client[i]], v_new_id, v_rel_client_contact_id);
                    END IF;

                    -- deal → contact (first 25 contacts paired with matching deal)
                    IF i <= 25 AND v_rel_deal_contact_id IS NOT NULL
                       AND i <= array_length(v_deal_ids, 1) THEN
                        INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                        VALUES (v_deal_ids[i], v_new_id, v_rel_deal_contact_id);
                    END IF;
                END LOOP;

                -- ── 40 tasks ──────────────────────────────────────────────────────
                FOR i IN 1..40 LOOP
                    IF v_task_type_id IS NULL THEN EXIT; END IF;

                    INSERT INTO entity (entity_type_id, is_archived, created_by_user_id)
                    VALUES (v_task_type_id, FALSE, v_creator_id)
                    RETURNING id INTO v_new_id;

                    INSERT INTO entity_workspace (entity_id, workspace_id)
                    VALUES (v_new_id, v_ws_ids[1 + ((i - 1) % v_ws_count)])
                    ON CONFLICT DO NOTHING;

                    INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                    SELECT v_new_id, prop, val_s, NULL, NULL, NULL, val_date
                    FROM (
                        SELECT v_prop_task_title    AS prop, t_title[i]  AS val_s, NULL::date AS val_date
                        UNION ALL SELECT v_prop_task_status,   t_status[i],  NULL
                        UNION ALL SELECT v_prop_task_priority, t_prio[i],    NULL
                        UNION ALL SELECT v_prop_task_type,     t_type[i],    NULL
                        UNION ALL SELECT v_prop_due_date,      NULL,         CURRENT_DATE + t_due_off[i]
                    ) AS vals(prop, val_s, val_date)
                    WHERE prop IS NOT NULL;

                    -- deal → task
                    IF v_rel_deal_task_id IS NOT NULL
                       AND t_deal_idx[i] >= 1 AND t_deal_idx[i] <= array_length(v_deal_ids, 1) THEN
                        INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                        VALUES (v_deal_ids[t_deal_idx[i]], v_new_id, v_rel_deal_task_id);
                    END IF;

                    -- client → task for tasks 30-40
                    IF i >= 30 AND v_rel_client_task_id IS NOT NULL THEN
                        INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                        VALUES (v_client_ids[1 + ((i - 30) % array_length(v_client_ids, 1))], v_new_id, v_rel_client_task_id);
                    END IF;
                END LOOP;

                -- ── 25 notes ──────────────────────────────────────────────────────
                FOR i IN 1..25 LOOP
                    IF v_note_type_id IS NULL THEN EXIT; END IF;

                    INSERT INTO entity (entity_type_id, is_archived, created_by_user_id)
                    VALUES (v_note_type_id, FALSE, v_creator_id)
                    RETURNING id INTO v_new_id;

                    INSERT INTO entity_workspace (entity_id, workspace_id)
                    VALUES (v_new_id, v_ws_ids[1 + ((i - 1) % v_ws_count)])
                    ON CONFLICT DO NOTHING;

                    INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date)
                    SELECT v_new_id, prop, val_s, NULL, NULL, NULL, val_date
                    FROM (
                        SELECT v_prop_note_content AS prop, n_content[i] AS val_s, NULL::date AS val_date
                        UNION ALL SELECT v_prop_note_date, NULL, CURRENT_DATE + n_date_off[i]
                    ) AS vals(prop, val_s, val_date)
                    WHERE prop IS NOT NULL;

                    -- deal → note
                    IF v_rel_deal_note_id IS NOT NULL
                       AND n_deal_idx[i] >= 1 AND n_deal_idx[i] <= array_length(v_deal_ids, 1) THEN
                        INSERT INTO entity_relationship (source_entity_id, target_entity_id, relationship_type_id)
                        VALUES (v_deal_ids[n_deal_idx[i]], v_new_id, v_rel_deal_note_id);
                    END IF;
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
                v_type_ids int[];
            BEGIN
                SELECT ARRAY_AGG(id) INTO v_type_ids
                FROM entity_type
                WHERE name IN ('deal','client','contact','task','note','deal_analysis','contract');

                IF v_type_ids IS NOT NULL THEN
                    DELETE FROM entity_relationship
                    WHERE source_entity_id IN (SELECT id FROM entity WHERE entity_type_id = ANY(v_type_ids) AND is_archived = FALSE)
                       OR target_entity_id IN (SELECT id FROM entity WHERE entity_type_id = ANY(v_type_ids) AND is_archived = FALSE);

                    DELETE FROM entity_property_value
                    WHERE entity_id IN (SELECT id FROM entity WHERE entity_type_id = ANY(v_type_ids) AND is_archived = FALSE);

                    DELETE FROM entity_workspace
                    WHERE entity_id IN (SELECT id FROM entity WHERE entity_type_id = ANY(v_type_ids) AND is_archived = FALSE);

                    DELETE FROM entity WHERE entity_type_id = ANY(v_type_ids) AND is_archived = FALSE;
                END IF;
            END $$;
            """
        );
    }
}
