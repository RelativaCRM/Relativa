using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDemoPasswordsToBcrypt : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword("Demo1234!");

            migrationBuilder.Sql($"""
                UPDATE users SET password = '{hash}'
                WHERE id IN (1, 2, 3);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE users SET password = '$2a$11$U0L3412xLEeQjOfrj5VGb.kPt.RAHBaV/lSNIbHesBuQc90DmFHfC'
                WHERE id = 1;

                UPDATE users SET password = '$2a$11$4J7luzuGBbWMQhuGnebPnu34QyUe867wkeBqahTtrVfjV0YMHNhqu'
                WHERE id = 2;

                UPDATE users SET password = '$2a$11$whaqAlWKw6kwO5K4hh2c5.DsjWOsSxIIP5QOLQK0/yZFWFZVDQMW2'
                WHERE id = 3;
                """);
        }
    }
}
