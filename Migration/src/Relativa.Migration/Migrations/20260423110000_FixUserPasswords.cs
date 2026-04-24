using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <summary>
    /// Replaces the placeholder bcrypt hashes that were inserted by the initial
    /// SeedData migration with real hashes so that seeded users can actually log in.
    /// All three users share the development password: Admin1234!
    /// </summary>
    public partial class FixUserPasswords : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE users SET password = '$2a$11$placeholder00000000000000000000000000000000000000000'
                WHERE id IN (1, 2, 3);
                """);
        }
    }
}
