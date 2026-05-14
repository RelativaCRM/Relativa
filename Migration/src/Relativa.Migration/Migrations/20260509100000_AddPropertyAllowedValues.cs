using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

[DbContext(typeof(MigrationDbContext))]
[Migration("20260509100000_AddPropertyAllowedValues")]
public partial class AddPropertyAllowedValues : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS property_allowed_value (
                property_id INTEGER NOT NULL REFERENCES property(id) ON DELETE CASCADE,
                value       TEXT    NOT NULL,
                PRIMARY KEY (property_id, value)
            );

            DO $$
            DECLARE
                v_prop_status int;
                v_prop_contract_status int;
            BEGIN
                SELECT id INTO v_prop_status
                FROM property WHERE name = 'status' AND organization_id IS NULL LIMIT 1;

                SELECT id INTO v_prop_contract_status
                FROM property WHERE name = 'contract_status' AND organization_id IS NULL LIMIT 1;

                IF v_prop_status IS NOT NULL THEN
                    INSERT INTO property_allowed_value (property_id, value)
                    VALUES
                        (v_prop_status, 'opened'),
                        (v_prop_status, 'pending'),
                        (v_prop_status, 'closed'),
                        (v_prop_status, 'revoked')
                    ON CONFLICT DO NOTHING;
                END IF;

                IF v_prop_contract_status IS NOT NULL THEN
                    INSERT INTO property_allowed_value (property_id, value)
                    VALUES
                        (v_prop_contract_status, 'active'),
                        (v_prop_contract_status, 'completed'),
                        (v_prop_contract_status, 'revoked')
                    ON CONFLICT DO NOTHING;
                END IF;
            END $$;
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS property_allowed_value;");
    }
}
