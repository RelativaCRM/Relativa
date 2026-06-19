using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

[DbContext(typeof(MigrationDbContext))]
[Migration("20260604200000_AddDisplayNameColumns")]
public partial class AddDisplayNameColumns : EfMigration
{
    /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "property",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "entity_type",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "property_allowed_value",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "entity_relationship_type",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "organization_roles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "workspace_roles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "permissions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // ── Populate display_name for system properties ───────────────────────
            migrationBuilder.Sql("""
                UPDATE property SET display_name = CASE name
                    WHEN 'first_name'                THEN 'First Name'
                    WHEN 'middle_name'               THEN 'Middle Name'
                    WHEN 'last_name'                 THEN 'Last Name'
                    WHEN 'phone_number'              THEN 'Phone Number'
                    WHEN 'email'                     THEN 'Email'
                    WHEN 'birth_date'                THEN 'Date of Birth'
                    WHEN 'country'                   THEN 'Country'
                    WHEN 'city'                      THEN 'City'
                    WHEN 'deal_value'                THEN 'Deal Value'
                    WHEN 'created_at'                THEN 'Created At'
                    WHEN 'status'                    THEN 'Status'
                    WHEN 'deal_stage'                THEN 'Deal Stage'
                    WHEN 'deal_source'               THEN 'Lead Source'
                    WHEN 'expected_close'            THEN 'Expected Close Date'
                    WHEN 'closure_score'             THEN 'Closure Score'
                    WHEN 'days_since_created'        THEN 'Days Since Created'
                    WHEN 'stage_encoded'             THEN 'Deal Stage (ML)'
                    WHEN 'num_interactions'          THEN 'Interaction Count'
                    WHEN 'days_since_last_contact'   THEN 'Days Since Last Contact'
                    WHEN 'num_open_deals'            THEN 'Open Deals Count'
                    WHEN 'avg_deal_value'            THEN 'Average Deal Value'
                    WHEN 'source_updated_at'         THEN 'Data Source Updated At'
                    WHEN 'calculated_at'             THEN 'Analysis Calculated At'
                    WHEN 'days_until_expected_close' THEN 'Days Until Close'
                    WHEN 'historical_close_rate'     THEN 'Historical Close Rate'
                    WHEN 'contract_number'           THEN 'Contract Number'
                    WHEN 'start_date'                THEN 'Start Date'
                    WHEN 'end_date'                  THEN 'End Date'
                    WHEN 'amount'                    THEN 'Amount'
                    WHEN 'currency'                  THEN 'Currency'
                    WHEN 'signed_at'                 THEN 'Signed Date'
                    WHEN 'contract_status'           THEN 'Contract Status'
                    WHEN 'contract_type'             THEN 'Contract Type'
                    WHEN 'title'                     THEN 'Title'
                    WHEN 'priority'                  THEN 'Priority'
                    WHEN 'company_name'              THEN 'Company Name'
                    WHEN 'industry'                  THEN 'Industry'
                    WHEN 'website'                   THEN 'Website'
                    WHEN 'annual_revenue'            THEN 'Annual Revenue'
                    WHEN 'employee_count'            THEN 'Employee Count'
                    WHEN 'client_status'             THEN 'Client Status'
                    WHEN 'client_lifetime_value'     THEN 'Lifetime Value'
                    WHEN 'client_tenure_days'        THEN 'Client Tenure (Days)'
                    WHEN 'job_title'                 THEN 'Job Title'
                    WHEN 'department'                THEN 'Department'
                    WHEN 'task_title'                THEN 'Task Title'
                    WHEN 'task_status'               THEN 'Status'
                    WHEN 'task_priority'             THEN 'Priority'
                    WHEN 'task_type'                 THEN 'Task Type'
                    WHEN 'due_date'                  THEN 'Due Date'
                    WHEN 'note_content'              THEN 'Content'
                    WHEN 'note_date'                 THEN 'Date'
                    ELSE NULL
                END
                WHERE organization_id IS NULL;
                """);

            // ── Populate display_name for entity types ────────────────────────────
            migrationBuilder.Sql("""
                UPDATE entity_type SET display_name = CASE name
                    WHEN 'client'        THEN 'Client'
                    WHEN 'deal'          THEN 'Deal'
                    WHEN 'deal_analysis' THEN 'Deal Analysis'
                    WHEN 'contract'      THEN 'Contract'
                    WHEN 'contact'       THEN 'Contact'
                    WHEN 'task'          THEN 'Task'
                    WHEN 'note'          THEN 'Note'
                    ELSE NULL
                END;
                """);

            // ── Populate display_name for property_allowed_value ──────────────────
            migrationBuilder.Sql("""
                UPDATE property_allowed_value pav
                SET display_name = CASE
                    WHEN p.name = 'status'          AND pav.value = 'opened'        THEN 'Open'
                    WHEN p.name = 'status'          AND pav.value = 'pending'       THEN 'Pending'
                    WHEN p.name = 'status'          AND pav.value = 'closed'        THEN 'Closed'
                    WHEN p.name = 'status'          AND pav.value = 'revoked'       THEN 'Revoked'
                    WHEN p.name = 'deal_stage'      AND pav.value = 'Prospecting'   THEN 'Prospecting'
                    WHEN p.name = 'deal_stage'      AND pav.value = 'Qualification' THEN 'Qualification'
                    WHEN p.name = 'deal_stage'      AND pav.value = 'Proposal'      THEN 'Proposal'
                    WHEN p.name = 'deal_stage'      AND pav.value = 'Negotiation'   THEN 'Negotiation'
                    WHEN p.name = 'deal_source'     AND pav.value = 'cold_outreach' THEN 'Cold Outreach'
                    WHEN p.name = 'deal_source'     AND pav.value = 'referral'      THEN 'Referral'
                    WHEN p.name = 'deal_source'     AND pav.value = 'website'       THEN 'Website'
                    WHEN p.name = 'deal_source'     AND pav.value = 'event'         THEN 'Event'
                    WHEN p.name = 'deal_source'     AND pav.value = 'partner'       THEN 'Partner'
                    WHEN p.name IN ('priority','task_priority') AND pav.value = 'high'   THEN 'High'
                    WHEN p.name IN ('priority','task_priority') AND pav.value = 'medium' THEN 'Medium'
                    WHEN p.name IN ('priority','task_priority') AND pav.value = 'low'    THEN 'Low'
                    WHEN p.name = 'industry'        AND pav.value = 'technology'    THEN 'Technology'
                    WHEN p.name = 'industry'        AND pav.value = 'finance'       THEN 'Finance & Banking'
                    WHEN p.name = 'industry'        AND pav.value = 'healthcare'    THEN 'Healthcare'
                    WHEN p.name = 'industry'        AND pav.value = 'retail'        THEN 'Retail'
                    WHEN p.name = 'industry'        AND pav.value = 'manufacturing' THEN 'Manufacturing'
                    WHEN p.name = 'industry'        AND pav.value = 'energy'        THEN 'Energy'
                    WHEN p.name = 'industry'        AND pav.value = 'education'     THEN 'Education'
                    WHEN p.name = 'industry'        AND pav.value = 'other'         THEN 'Other'
                    WHEN p.name = 'employee_count'  AND pav.value = '1-10'          THEN '1 – 10'
                    WHEN p.name = 'employee_count'  AND pav.value = '11-50'         THEN '11 – 50'
                    WHEN p.name = 'employee_count'  AND pav.value = '51-200'        THEN '51 – 200'
                    WHEN p.name = 'employee_count'  AND pav.value = '201-1000'      THEN '201 – 1,000'
                    WHEN p.name = 'employee_count'  AND pav.value = '1000+'         THEN '1,000+'
                    WHEN p.name = 'client_status'   AND pav.value = 'lead'          THEN 'Lead'
                    WHEN p.name = 'client_status'   AND pav.value = 'prospect'      THEN 'Prospect'
                    WHEN p.name = 'client_status'   AND pav.value = 'active'        THEN 'Active'
                    WHEN p.name = 'client_status'   AND pav.value = 'at_risk'       THEN 'At Risk'
                    WHEN p.name = 'client_status'   AND pav.value = 'churned'       THEN 'Churned'
                    WHEN p.name = 'task_status'     AND pav.value = 'todo'          THEN 'To Do'
                    WHEN p.name = 'task_status'     AND pav.value = 'in_progress'   THEN 'In Progress'
                    WHEN p.name = 'task_status'     AND pav.value = 'done'          THEN 'Done'
                    WHEN p.name = 'task_status'     AND pav.value = 'cancelled'     THEN 'Cancelled'
                    WHEN p.name = 'task_type'       AND pav.value = 'call'          THEN 'Call'
                    WHEN p.name = 'task_type'       AND pav.value = 'meeting'       THEN 'Meeting'
                    WHEN p.name = 'task_type'       AND pav.value = 'email'         THEN 'Email'
                    WHEN p.name = 'task_type'       AND pav.value = 'follow_up'     THEN 'Follow-Up'
                    WHEN p.name = 'task_type'       AND pav.value = 'demo'          THEN 'Demo'
                    WHEN p.name = 'contract_status' AND pav.value = 'active'        THEN 'Active'
                    WHEN p.name = 'contract_status' AND pav.value = 'completed'     THEN 'Completed'
                    WHEN p.name = 'contract_status' AND pav.value = 'revoked'       THEN 'Revoked'
                    WHEN p.name = 'contract_type'   AND pav.value = 'subscription'  THEN 'Subscription'
                    WHEN p.name = 'contract_type'   AND pav.value = 'one_time'      THEN 'One-Time'
                    WHEN p.name = 'contract_type'   AND pav.value = 'retainer'      THEN 'Retainer'
                    ELSE NULL
                END
                FROM property p
                WHERE pav.property_id = p.id
                  AND p.organization_id IS NULL;
                """);

            // ── Populate display_name for entity_relationship_type ────────────────
            migrationBuilder.Sql("""
                UPDATE entity_relationship_type SET display_name = CASE name
                    WHEN 'deal_client'    THEN 'Client'
                    WHEN 'deal_analysis'  THEN 'Analysis'
                    WHEN 'contract_deal'  THEN 'Deal'
                    WHEN 'client_contact' THEN 'Contacts'
                    WHEN 'deal_contact'   THEN 'Contacts'
                    WHEN 'deal_task'      THEN 'Tasks'
                    WHEN 'client_task'    THEN 'Tasks'
                    WHEN 'deal_note'      THEN 'Notes'
                    WHEN 'client_note'    THEN 'Notes'
                    ELSE NULL
                END;
                """);

            // ── Populate display_name for system organization roles ───────────────
            migrationBuilder.Sql("""
                UPDATE organization_roles SET display_name = CASE name
                    WHEN 'org_owner'  THEN 'Owner'
                    WHEN 'org_admin'  THEN 'Administrator'
                    WHEN 'org_member' THEN 'Member'
                    ELSE NULL
                END
                WHERE organization_id IS NULL;
                """);

            // ── Populate display_name for system workspace roles ──────────────────
            migrationBuilder.Sql("""
                UPDATE workspace_roles SET display_name = CASE name
                    WHEN 'ws_admin'   THEN 'Administrator'
                    WHEN 'ws_manager' THEN 'Manager'
                    WHEN 'ws_analyst' THEN 'Analyst'
                    WHEN 'ws_member'  THEN 'Member'
                    ELSE NULL
                END
                WHERE workspace_id IS NULL;
                """);

            // ── Populate display_name for permissions ─────────────────────────────
            migrationBuilder.Sql("""
                UPDATE permissions SET display_name = CASE name
                    WHEN 'manage_org_settings'    THEN 'Manage Organization Settings'
                    WHEN 'invite_to_org'          THEN 'Invite to Organization'
                    WHEN 'manage_join_requests'   THEN 'Manage Join Requests'
                    WHEN 'remove_org_members'     THEN 'Remove Members'
                    WHEN 'assign_org_roles'       THEN 'Assign Roles'
                    WHEN 'manage_org_roles'       THEN 'Manage Roles'
                    WHEN 'create_workspaces'      THEN 'Create Workspaces'
                    WHEN 'manage_ws_settings'     THEN 'Manage Workspace Settings'
                    WHEN 'invite_to_workspace'    THEN 'Invite to Workspace'
                    WHEN 'add_ws_members'         THEN 'Add Members'
                    WHEN 'remove_ws_members'      THEN 'Remove Members'
                    WHEN 'assign_ws_roles'        THEN 'Assign Roles'
                    WHEN 'manage_ws_roles'        THEN 'Manage Roles'
                    WHEN 'manage_entities'        THEN 'Manage Records'
                    WHEN 'view_entities'          THEN 'View Records'
                    WHEN 'view_analytics'         THEN 'View Analytics'
                    WHEN 'edit_archived_entities' THEN 'Edit Archived Records'
                    WHEN 'view_basic_stats'       THEN 'View Basic Stats'
                    WHEN 'view_team_analytics'    THEN 'View Team Analytics'
                    ELSE NULL
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "display_name", table: "property");
            migrationBuilder.DropColumn(name: "display_name", table: "entity_type");
            migrationBuilder.DropColumn(name: "display_name", table: "property_allowed_value");
            migrationBuilder.DropColumn(name: "display_name", table: "entity_relationship_type");
            migrationBuilder.DropColumn(name: "display_name", table: "organization_roles");
            migrationBuilder.DropColumn(name: "display_name", table: "workspace_roles");
            migrationBuilder.DropColumn(name: "display_name", table: "permissions");
        }
}
