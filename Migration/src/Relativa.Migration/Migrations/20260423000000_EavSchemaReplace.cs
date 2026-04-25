using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class EavSchemaReplace : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ----------------------------------------------------------------
            // 1. Drop old polymorphic property tables (FK order matters)
            // ----------------------------------------------------------------
            migrationBuilder.DropTable(name: "entity_properties");
            migrationBuilder.DropTable(name: "deal_property_values");
            migrationBuilder.DropTable(name: "location_property_values");
            migrationBuilder.DropTable(name: "personal_data_property_values");

            // ----------------------------------------------------------------
            // 2. Rename entity_types → entity_type
            //    Drop type_id + is_archived, add name
            // ----------------------------------------------------------------
            migrationBuilder.RenameTable(name: "entity_types", newName: "entity_type");
            migrationBuilder.RenameIndex(
                name: "IX_entity_types_type_id",
                table: "entity_type",
                newName: "IX_entity_type_type_id");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "entity_type",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("UPDATE entity_type SET name = type_id WHERE name IS NULL;");

            migrationBuilder.DropColumn(name: "type_id", table: "entity_type");
            migrationBuilder.DropColumn(name: "is_archived", table: "entity_type");

            migrationBuilder.Sql("ALTER TABLE entity_type ALTER COLUMN name SET NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "ix_entity_type_name",
                table: "entity_type",
                column: "name",
                unique: true);

            // ----------------------------------------------------------------
            // 3. Rename entities → entity; rename FK column type → entity_type_id
            // ----------------------------------------------------------------
            migrationBuilder.DropForeignKey(
                name: "fk_entities_type",
                table: "entities");

            migrationBuilder.RenameTable(name: "entities", newName: "entity");
            migrationBuilder.RenameIndex(
                name: "IX_entities_type",
                table: "entity",
                newName: "IX_entity_entity_type_id");

            migrationBuilder.RenameColumn(
                name: "type",
                table: "entity",
                newName: "entity_type_id");

            migrationBuilder.AddForeignKey(
                name: "fk_entity_entity_type",
                table: "entity",
                column: "entity_type_id",
                principalTable: "entity_type",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // ----------------------------------------------------------------
            // 4. Rename entity_workspaces → entity_workspace
            // ----------------------------------------------------------------
            migrationBuilder.DropForeignKey(name: "fk_ew_entity", table: "entity_workspaces");
            migrationBuilder.DropForeignKey(name: "fk_ew_workspace", table: "entity_workspaces");

            migrationBuilder.RenameTable(name: "entity_workspaces", newName: "entity_workspace");
            migrationBuilder.RenameIndex(
                name: "IX_entity_workspaces_workspace_id",
                table: "entity_workspace",
                newName: "IX_entity_workspace_workspace_id");
            migrationBuilder.RenameIndex(
                name: "IX_entity_workspaces_entity_id",
                table: "entity_workspace",
                newName: "IX_entity_workspace_entity_id");

            migrationBuilder.AddForeignKey(
                name: "fk_ew_entity",
                table: "entity_workspace",
                column: "entity_id",
                principalTable: "entity",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_ew_workspace",
                table: "entity_workspace",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // ----------------------------------------------------------------
            // 5. Create property table
            // ----------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "property",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    data_type = table.Column<string>(type: "text", nullable: false),
                    organization_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property", x => x.id);
                    table.ForeignKey(
                        name: "fk_property_organization",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_property_organization_id",
                table: "property",
                column: "organization_id");

            // ----------------------------------------------------------------
            // 6. Create entity_type_property table (composite PK)
            // ----------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "entity_type_property",
                columns: table => new
                {
                    entity_type_id = table.Column<int>(type: "integer", nullable: false),
                    property_id = table.Column<int>(type: "integer", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_type_property", x => new { x.entity_type_id, x.property_id });
                    table.ForeignKey(
                        name: "fk_etp_entity_type",
                        column: x => x.entity_type_id,
                        principalTable: "entity_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_etp_property",
                        column: x => x.property_id,
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_entity_type_property_property_id",
                table: "entity_type_property",
                column: "property_id");

            // ----------------------------------------------------------------
            // 7. Create entity_property_value table (composite PK)
            // ----------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "entity_property_value",
                columns: table => new
                {
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    property_id = table.Column<int>(type: "integer", nullable: false),
                    value_string = table.Column<string>(type: "text", nullable: true),
                    value_int = table.Column<int>(type: "integer", nullable: true),
                    value_decimal = table.Column<decimal>(type: "numeric", nullable: true),
                    value_bool = table.Column<bool>(type: "boolean", nullable: true),
                    value_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_property_value", x => new { x.entity_id, x.property_id });
                    table.ForeignKey(
                        name: "fk_epv_entity",
                        column: x => x.entity_id,
                        principalTable: "entity",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_epv_property",
                        column: x => x.property_id,
                        principalTable: "property",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_epv_entity_id",
                table: "entity_property_value",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_epv_property_id",
                table: "entity_property_value",
                column: "property_id");

            // ----------------------------------------------------------------
            // 8. Create entity_relationship_type table
            // ----------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "entity_relationship_type",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    source_entity_type_id = table.Column<int>(type: "integer", nullable: false),
                    target_entity_type_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_relationship_type", x => x.id);
                    table.ForeignKey(
                        name: "fk_ert_source_entity_type",
                        column: x => x.source_entity_type_id,
                        principalTable: "entity_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ert_target_entity_type",
                        column: x => x.target_entity_type_id,
                        principalTable: "entity_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_entity_relationship_type_name",
                table: "entity_relationship_type",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_entity_relationship_type_source_entity_type_id",
                table: "entity_relationship_type",
                column: "source_entity_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_entity_relationship_type_target_entity_type_id",
                table: "entity_relationship_type",
                column: "target_entity_type_id");

            // ----------------------------------------------------------------
            // 9. Create entity_relationship table
            // ----------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "entity_relationship",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    source_entity_id = table.Column<int>(type: "integer", nullable: false),
                    target_entity_id = table.Column<int>(type: "integer", nullable: false),
                    relationship_type_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_relationship", x => x.id);
                    table.ForeignKey(
                        name: "fk_er_source_entity",
                        column: x => x.source_entity_id,
                        principalTable: "entity",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_er_target_entity",
                        column: x => x.target_entity_id,
                        principalTable: "entity",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_er_relationship_type",
                        column: x => x.relationship_type_id,
                        principalTable: "entity_relationship_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_er_source_entity_id",
                table: "entity_relationship",
                column: "source_entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_er_relationship_type_id",
                table: "entity_relationship",
                column: "relationship_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_entity_relationship_target_entity_id",
                table: "entity_relationship",
                column: "target_entity_id");

            // ----------------------------------------------------------------
            // 10. Seed EAV tables (SeedData runs before this migration and only
            //     targets InitialCreate schema — entity rows survive renames above)
            // ----------------------------------------------------------------
            migrationBuilder.Sql(@"
INSERT INTO property (id, name, data_type, organization_id) VALUES
(1,  'first_name',    'String',  NULL),
(2,  'middle_name',   'String',  NULL),
(3,  'last_name',     'String',  NULL),
(4,  'phone_number',  'String',  NULL),
(5,  'email',         'String',  NULL),
(6,  'birth_date',    'Date',    NULL),
(7,  'country',       'String',  NULL),
(8,  'city',          'String',  NULL),
(9,  'deal_value',    'Decimal', NULL),
(10, 'expected_close','Date',    NULL),
(11, 'closure_score', 'Decimal', NULL);

INSERT INTO entity_type_property (entity_type_id, property_id, is_required) VALUES
(1, 1,  TRUE), (1, 2,  FALSE), (1, 3,  TRUE), (1, 4,  FALSE), (1, 5,  FALSE), (1, 6,  FALSE), (1, 7,  FALSE), (1, 8,  FALSE);
INSERT INTO entity_type_property (entity_type_id, property_id, is_required) VALUES
(2, 9,  FALSE), (2, 10, FALSE), (2, 11, FALSE);

INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date) VALUES
(1, 1,  'Oleksiy',           NULL, NULL, NULL, NULL),
(1, 3,  'Ivanenko',          NULL, NULL, NULL, NULL),
(1, 4,  '+380671234567',     NULL, NULL, NULL, NULL),
(1, 5,  'o.ivanenko@tech.ua',NULL, NULL, NULL, NULL),
(1, 6,  NULL,                NULL, NULL, NULL, '1985-05-20'),
(1, 7,  'Ukraine',           NULL, NULL, NULL, NULL),
(1, 8,  'Kyiv',              NULL, NULL, NULL, NULL);
INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date) VALUES
(2, 1,  'Maria',             NULL, NULL, NULL, NULL),
(2, 3,  'Zankovetska',       NULL, NULL, NULL, NULL),
(2, 4,  '+380501234567',     NULL, NULL, NULL, NULL),
(2, 5,  'm.zan@corp.ua',     NULL, NULL, NULL, NULL),
(2, 6,  NULL,                NULL, NULL, NULL, '1990-11-15'),
(2, 7,  'Ukraine',           NULL, NULL, NULL, NULL),
(2, 8,  'Lviv',              NULL, NULL, NULL, NULL);
INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date) VALUES
(3, 9,  NULL, NULL, 25000.00, NULL, NULL),
(3, 10, NULL, NULL, NULL,     NULL, '2026-06-30'),
(3, 11, NULL, NULL, 0.74,     NULL, NULL);
INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date) VALUES
(4, 9,  NULL, NULL, 15000.00, NULL, NULL),
(4, 10, NULL, NULL, NULL,     NULL, '2026-05-15'),
(4, 11, NULL, NULL, 0.85,     NULL, NULL);
INSERT INTO entity_property_value (entity_id, property_id, value_string, value_int, value_decimal, value_bool, value_date) VALUES
(5, 9,  NULL, NULL, 50000.00, NULL, NULL),
(5, 10, NULL, NULL, NULL,     NULL, '2026-08-01'),
(5, 11, NULL, NULL, 0.45,     NULL, NULL);

INSERT INTO entity_relationship_type (id, name, source_entity_type_id, target_entity_type_id) VALUES
(1, 'deal_client', 2, 1);
INSERT INTO entity_relationship (id, source_entity_id, target_entity_id, relationship_type_id) VALUES
(1, 3, 1, 1), (2, 4, 2, 1), (3, 5, 1, 1);

SELECT setval(pg_get_serial_sequence('property', 'id'), (SELECT COALESCE(MAX(id),1) FROM property));
SELECT setval(pg_get_serial_sequence('entity_relationship_type', 'id'), (SELECT COALESCE(MAX(id),1) FROM entity_relationship_type));
SELECT setval(pg_get_serial_sequence('entity_relationship', 'id'), (SELECT COALESCE(MAX(id),1) FROM entity_relationship));
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ----------------------------------------------------------------
            // Drop new EAV tables (reverse dependency order)
            // ----------------------------------------------------------------
            migrationBuilder.DropTable(name: "entity_relationship");
            migrationBuilder.DropTable(name: "entity_relationship_type");
            migrationBuilder.DropTable(name: "entity_property_value");
            migrationBuilder.DropTable(name: "entity_type_property");
            migrationBuilder.DropTable(name: "property");

            // ----------------------------------------------------------------
            // Rename entity_workspace back to entity_workspaces
            // ----------------------------------------------------------------
            migrationBuilder.DropForeignKey(name: "fk_ew_entity", table: "entity_workspace");
            migrationBuilder.DropForeignKey(name: "fk_ew_workspace", table: "entity_workspace");

            migrationBuilder.RenameTable(name: "entity_workspace", newName: "entity_workspaces");
            migrationBuilder.RenameIndex(
                name: "IX_entity_workspace_workspace_id",
                table: "entity_workspaces",
                newName: "IX_entity_workspaces_workspace_id");
            migrationBuilder.RenameIndex(
                name: "IX_entity_workspace_entity_id",
                table: "entity_workspaces",
                newName: "IX_entity_workspaces_entity_id");

            migrationBuilder.AddForeignKey(
                name: "fk_ew_entity",
                table: "entity_workspaces",
                column: "entity_id",
                principalTable: "entities",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_ew_workspace",
                table: "entity_workspaces",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // ----------------------------------------------------------------
            // Rename entity back to entities; restore FK column name
            // ----------------------------------------------------------------
            migrationBuilder.DropForeignKey(name: "fk_entity_entity_type", table: "entity");

            migrationBuilder.RenameColumn(name: "entity_type_id", table: "entity", newName: "type");
            migrationBuilder.RenameTable(name: "entity", newName: "entities");
            migrationBuilder.RenameIndex(
                name: "IX_entity_entity_type_id",
                table: "entities",
                newName: "IX_entities_type");

            migrationBuilder.AddForeignKey(
                name: "fk_entities_type",
                table: "entities",
                column: "type",
                principalTable: "entity_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // ----------------------------------------------------------------
            // Rename entity_type back to entity_types; restore columns
            // ----------------------------------------------------------------
            migrationBuilder.DropIndex(name: "ix_entity_type_name", table: "entity_type");
            migrationBuilder.DropColumn(name: "name", table: "entity_type");

            migrationBuilder.RenameTable(name: "entity_type", newName: "entity_types");

            migrationBuilder.AddColumn<string>(
                name: "type_id",
                table: "entity_types",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                table: "entity_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_entity_types_type_id",
                table: "entity_types",
                column: "type_id",
                unique: true);

            // ----------------------------------------------------------------
            // Recreate old property tables
            // ----------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "personal_data_property_values",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    passport_number = table.Column<string>(type: "text", nullable: true),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personal_data_property_values", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "location_property_values",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    country = table.Column<string>(type: "text", nullable: true),
                    region = table.Column<string>(type: "text", nullable: true),
                    state = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    postal_code = table.Column<string>(type: "text", nullable: true),
                    locale = table.Column<string>(type: "text", nullable: true),
                    timezone = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location_property_values", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deal_property_values",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    value = table.Column<decimal>(type: "numeric", nullable: true),
                    owner_id = table.Column<int>(type: "integer", nullable: true),
                    client_id = table.Column<int>(type: "integer", nullable: false),
                    expected_close = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closure_score = table.Column<decimal>(type: "numeric", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deal_property_values", x => x.id);
                    table.ForeignKey(
                        name: "fk_deal_client",
                        column: x => x.client_id,
                        principalTable: "entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_deal_owner",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "entity_properties",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    personal_data_property_id = table.Column<int>(type: "integer", nullable: true),
                    location_property_id = table.Column<int>(type: "integer", nullable: true),
                    deal_property_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_properties", x => x.id);
                    table.ForeignKey(
                        name: "fk_ep_entity",
                        column: x => x.entity_id,
                        principalTable: "entities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ep_personal",
                        column: x => x.personal_data_property_id,
                        principalTable: "personal_data_property_values",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_ep_location",
                        column: x => x.location_property_id,
                        principalTable: "location_property_values",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_ep_deal",
                        column: x => x.deal_property_id,
                        principalTable: "deal_property_values",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });
        }
    }
}
