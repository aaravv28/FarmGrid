using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmGrid.Data.Migrations
{
    /// <inheritdoc />
    public partial class OwnersAreRealUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Owners become foreign keys to real users. Rows owned by a missing or
            // placeholder id (seed-farmer-N, demo-farmer-id, anonymous-farmer, NULL) are
            // adopted by the demo farmer, or else the earliest Farmer; they can't simply be
            // deleted because past orders reference them. Orphaned trip participants are
            // removed and their cargo returned to the trip.
            migrationBuilder.Sql("""
                DECLARE @adopter nvarchar(450) = (
                    SELECT TOP 1 u.Id
                    FROM AspNetUsers u
                    JOIN AspNetUserRoles ur ON ur.UserId = u.Id
                    JOIN AspNetRoles r ON r.Id = ur.RoleId AND r.Name = 'Farmer'
                    ORDER BY CASE WHEN u.NormalizedEmail = 'FARMER@FARMGRID.COM' THEN 0 ELSE 1 END, u.CreatedAt);

                IF @adopter IS NULL AND (
                       EXISTS (SELECT 1 FROM Products WHERE FarmerId IS NULL OR FarmerId NOT IN (SELECT Id FROM AspNetUsers))
                    OR EXISTS (SELECT 1 FROM QuickSellListings WHERE FarmerId NOT IN (SELECT Id FROM AspNetUsers))
                    OR EXISTS (SELECT 1 FROM TransportTrips WHERE FarmerId NOT IN (SELECT Id FROM AspNetUsers)))
                    THROW 50000, 'Some products, Quick Sell lots or trips are owned by ids that are not users, and no Farmer user exists to adopt them. Create a Farmer account, then run the migration again.', 1;

                UPDATE Products SET FarmerId = @adopter
                WHERE FarmerId IS NULL OR FarmerId NOT IN (SELECT Id FROM AspNetUsers);

                UPDATE QuickSellListings
                SET FarmerId = @adopter, FarmerName = (SELECT FullName FROM AspNetUsers WHERE Id = @adopter)
                WHERE FarmerId NOT IN (SELECT Id FROM AspNetUsers);

                UPDATE TransportTrips SET FarmerId = @adopter
                WHERE FarmerId NOT IN (SELECT Id FROM AspNetUsers);

                UPDATE Orders SET FarmerId = @adopter
                WHERE FarmerId IS NOT NULL AND FarmerId NOT IN (SELECT Id FROM AspNetUsers);

                -- A trip's host row follows the trip's (now real) owner, unless they already ride on it
                UPDATE p SET FarmerId = t.FarmerId
                FROM TransportParticipants p
                JOIN TransportTrips t ON t.Id = p.TransportTripId
                WHERE p.IsHost = 1
                  AND p.FarmerId NOT IN (SELECT Id FROM AspNetUsers)
                  AND NOT EXISTS (SELECT 1 FROM TransportParticipants q
                                  WHERE q.TransportTripId = p.TransportTripId AND q.FarmerId = t.FarmerId);

                UPDATE t SET AvailableCapacityKg = t.AvailableCapacityKg + o.ReturnedKg
                FROM TransportTrips t
                JOIN (SELECT TransportTripId, SUM(CargoWeightKg) AS ReturnedKg
                      FROM TransportParticipants
                      WHERE IsHost = 0 AND FarmerId NOT IN (SELECT Id FROM AspNetUsers)
                      GROUP BY TransportTripId) o ON o.TransportTripId = t.Id;

                DELETE FROM TransportParticipants WHERE FarmerId NOT IN (SELECT Id FROM AspNetUsers);
                """);

            migrationBuilder.AlterColumn<string>(
                name: "FarmerId",
                table: "TransportTrips",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FarmerId",
                table: "QuickSellListings",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FarmerId",
                table: "Products",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FarmerId",
                table: "Orders",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransportTrips_FarmerId",
                table: "TransportTrips",
                column: "FarmerId");

            migrationBuilder.CreateIndex(
                name: "IX_TransportParticipants_FarmerId",
                table: "TransportParticipants",
                column: "FarmerId");

            migrationBuilder.CreateIndex(
                name: "IX_QuickSellListings_FarmerId",
                table: "QuickSellListings",
                column: "FarmerId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_FarmerId",
                table: "Products",
                column: "FarmerId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_FarmerId",
                table: "Orders",
                column: "FarmerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_AspNetUsers_FarmerId",
                table: "Orders",
                column: "FarmerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_AspNetUsers_FarmerId",
                table: "Products",
                column: "FarmerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuickSellListings_AspNetUsers_FarmerId",
                table: "QuickSellListings",
                column: "FarmerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TransportParticipants_AspNetUsers_FarmerId",
                table: "TransportParticipants",
                column: "FarmerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TransportTrips_AspNetUsers_FarmerId",
                table: "TransportTrips",
                column: "FarmerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_AspNetUsers_FarmerId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_AspNetUsers_FarmerId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_QuickSellListings_AspNetUsers_FarmerId",
                table: "QuickSellListings");

            migrationBuilder.DropForeignKey(
                name: "FK_TransportParticipants_AspNetUsers_FarmerId",
                table: "TransportParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_TransportTrips_AspNetUsers_FarmerId",
                table: "TransportTrips");

            migrationBuilder.DropIndex(
                name: "IX_TransportTrips_FarmerId",
                table: "TransportTrips");

            migrationBuilder.DropIndex(
                name: "IX_TransportParticipants_FarmerId",
                table: "TransportParticipants");

            migrationBuilder.DropIndex(
                name: "IX_QuickSellListings_FarmerId",
                table: "QuickSellListings");

            migrationBuilder.DropIndex(
                name: "IX_Products_FarmerId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Orders_FarmerId",
                table: "Orders");

            migrationBuilder.AlterColumn<string>(
                name: "FarmerId",
                table: "TransportTrips",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "FarmerId",
                table: "QuickSellListings",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "FarmerId",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "FarmerId",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
