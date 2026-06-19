using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

[DbContext(typeof(MigrationDbContext))]
[Migration("20260618000001_FixEntityTypePropertyRequiredFlags")]
public partial class FixEntityTypePropertyRequiredFlags : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // deal: created_at and status are system/auto fields, not user-entered
        migrationBuilder.Sql(
            """
            UPDATE entity_type_property etp
            SET    is_required = FALSE
            FROM   entity_type et,
                   property    p
            WHERE  etp.entity_type_id = et.id
              AND  etp.property_id    = p.id
              AND  et.name = 'deal'
              AND  p.name  IN ('created_at', 'status')
              AND  p.organization_id IS NULL;
            """
        );

        // deal_analysis: ML computed fields — never user-entered
        migrationBuilder.Sql(
            """
            UPDATE entity_type_property etp
            SET    is_required = FALSE
            FROM   entity_type et,
                   property    p
            WHERE  etp.entity_type_id = et.id
              AND  etp.property_id    = p.id
              AND  et.name = 'deal_analysis'
              AND  p.name  IN ('days_since_created', 'stage_encoded', 'num_interactions',
                               'days_since_last_contact', 'num_open_deals', 'avg_deal_value',
                               'source_updated_at', 'calculated_at')
              AND  p.organization_id IS NULL;
            """
        );

        // contract: nested-modal UX — none should block creation
        migrationBuilder.Sql(
            """
            UPDATE entity_type_property etp
            SET    is_required = FALSE
            FROM   entity_type et,
                   property    p
            WHERE  etp.entity_type_id = et.id
              AND  etp.property_id    = p.id
              AND  et.name = 'contract'
              AND  p.name  IN ('contract_number', 'start_date', 'end_date', 'amount',
                               'currency', 'signed_at', 'contract_status')
              AND  p.organization_id IS NULL;
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE entity_type_property etp
            SET    is_required = TRUE
            FROM   entity_type et,
                   property    p
            WHERE  etp.entity_type_id = et.id
              AND  etp.property_id    = p.id
              AND  et.name = 'deal'
              AND  p.name  IN ('created_at', 'status')
              AND  p.organization_id IS NULL;
            """
        );

        migrationBuilder.Sql(
            """
            UPDATE entity_type_property etp
            SET    is_required = TRUE
            FROM   entity_type et,
                   property    p
            WHERE  etp.entity_type_id = et.id
              AND  etp.property_id    = p.id
              AND  et.name = 'deal_analysis'
              AND  p.name  IN ('days_since_created', 'stage_encoded', 'num_interactions',
                               'days_since_last_contact', 'num_open_deals', 'avg_deal_value',
                               'source_updated_at', 'calculated_at')
              AND  p.organization_id IS NULL;
            """
        );

        migrationBuilder.Sql(
            """
            UPDATE entity_type_property etp
            SET    is_required = TRUE
            FROM   entity_type et,
                   property    p
            WHERE  etp.entity_type_id = et.id
              AND  etp.property_id    = p.id
              AND  et.name = 'contract'
              AND  p.name  IN ('contract_number', 'start_date', 'end_date', 'amount',
                               'currency', 'signed_at', 'contract_status')
              AND  p.organization_id IS NULL;
            """
        );
    }
}
