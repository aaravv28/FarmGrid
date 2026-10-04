using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmGrid.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Identity roles become the only record of a user's role. Login used to
            // copy UserRole into AspNetUserRoles lazily, so backfill anyone never synced.
            migrationBuilder.Sql("""
                INSERT INTO AspNetUserRoles (UserId, RoleId)
                SELECT u.Id, r.Id
                FROM AspNetUsers u
                JOIN AspNetRoles r ON r.Name = u.UserRole
                WHERE NOT EXISTS (
                    SELECT 1 FROM AspNetUserRoles ur
                    WHERE ur.UserId = u.Id AND ur.RoleId = r.Id);
                """);

            migrationBuilder.DropColumn(
                name: "UserRole",
                table: "AspNetUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserRole",
                table: "AspNetUsers",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");
        }
    }
}
