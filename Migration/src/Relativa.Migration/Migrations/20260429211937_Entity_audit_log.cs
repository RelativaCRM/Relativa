using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class Entity_audit_log : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "entity_audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: true),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    changed_by = table.Column<int>(type: "integer", nullable: true),
                    field_name = table.Column<string>(type: "text", nullable: true),
                    old_value = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    new_value = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_entity_audit_log_entities",
                        column: x => x.entity_id,
                        principalTable: "entity",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_entity_audit_log_users",
                        column: x => x.changed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "organization_audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<int>(type: "integer", nullable: true),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    changed_by = table.Column<int>(type: "integer", nullable: true),
                    field_name = table.Column<string>(type: "text", nullable: true),
                    old_value = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    new_value = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_organization_audit_log_organizations",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_organization_audit_log_users",
                        column: x => x.changed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_user_id = table.Column<int>(type: "integer", nullable: true),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    changed_by = table.Column<int>(type: "integer", nullable: true),
                    field_name = table.Column<string>(type: "text", nullable: true),
                    old_value = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    new_value = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_audit_log_target_users",
                        column: x => x.target_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_user_audit_log_users",
                        column: x => x.changed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "workspace_audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<int>(type: "integer", nullable: true),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    changed_by = table.Column<int>(type: "integer", nullable: true),
                    field_name = table.Column<string>(type: "text", nullable: true),
                    old_value = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    new_value = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_workspace_audit_log_users",
                        column: x => x.changed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_workspace_audit_log_workspaces",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_entity_audit_log_changed_at",
                table: "entity_audit_log",
                column: "changed_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_entity_audit_log_changed_by",
                table: "entity_audit_log",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_entity_audit_log_entity_id",
                table: "entity_audit_log",
                column: "entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_organization_audit_log_changed_at",
                table: "organization_audit_log",
                column: "changed_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_organization_audit_log_changed_by",
                table: "organization_audit_log",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_organization_audit_log_organization_id",
                table: "organization_audit_log",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_audit_log_changed_at",
                table: "user_audit_log",
                column: "changed_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_user_audit_log_changed_by",
                table: "user_audit_log",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_user_audit_log_target_user_id",
                table: "user_audit_log",
                column: "target_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_audit_log_changed_at",
                table: "workspace_audit_log",
                column: "changed_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_workspace_audit_log_changed_by",
                table: "workspace_audit_log",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_audit_log_workspace_id",
                table: "workspace_audit_log",
                column: "workspace_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entity_audit_log");

            migrationBuilder.DropTable(
                name: "organization_audit_log");

            migrationBuilder.DropTable(
                name: "user_audit_log");

            migrationBuilder.DropTable(
                name: "workspace_audit_log");
        }
    }
}
