using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class MakeEntityCreatedByUserNullable : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_entity_created_by_user",
                table: "entity");

            migrationBuilder.DropIndex(
                name: "ix_entity_created_by_user",
                table: "entity");

            migrationBuilder.AlterColumn<int>(
                name: "created_by_user_id",
                table: "entity",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "created_by_user_id",
                table: "entity",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_entity_created_by_user",
                table: "entity",
                column: "created_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_entity_created_by_user",
                table: "entity",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
