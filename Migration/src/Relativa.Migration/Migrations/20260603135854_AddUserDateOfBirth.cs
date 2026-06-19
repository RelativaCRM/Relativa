using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDateOfBirth : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "date_of_birth",
                table: "users",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "date_of_birth",
                table: "users");
        }
    }
}
