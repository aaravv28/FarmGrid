using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmGrid.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueTripParticipantAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransportParticipants_TransportTripId",
                table: "TransportParticipants");

            migrationBuilder.AlterColumn<string>(
                name: "FarmerId",
                table: "TransportParticipants",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            // A farmer may appear on a trip only once. Remove duplicate joins left by
            // earlier double-submits (keeping the first) and return their cargo to the trip.
            migrationBuilder.Sql("""
                WITH Duplicates AS (
                    SELECT Id, TransportTripId, CargoWeightKg,
                           ROW_NUMBER() OVER (PARTITION BY TransportTripId, FarmerId ORDER BY Id) AS RowNumber
                    FROM TransportParticipants
                )
                UPDATE t SET AvailableCapacityKg = t.AvailableCapacityKg + d.ReturnedKg
                FROM TransportTrips t
                JOIN (SELECT TransportTripId, SUM(CargoWeightKg) AS ReturnedKg
                      FROM Duplicates WHERE RowNumber > 1
                      GROUP BY TransportTripId) d ON d.TransportTripId = t.Id;

                WITH Duplicates AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (PARTITION BY TransportTripId, FarmerId ORDER BY Id) AS RowNumber
                    FROM TransportParticipants
                )
                DELETE FROM Duplicates WHERE RowNumber > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TransportParticipants_TransportTripId_FarmerId",
                table: "TransportParticipants",
                columns: new[] { "TransportTripId", "FarmerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransportParticipants_TransportTripId_FarmerId",
                table: "TransportParticipants");

            migrationBuilder.AlterColumn<string>(
                name: "FarmerId",
                table: "TransportParticipants",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_TransportParticipants_TransportTripId",
                table: "TransportParticipants",
                column: "TransportTripId");
        }
    }
}
