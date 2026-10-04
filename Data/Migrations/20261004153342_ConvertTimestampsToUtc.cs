using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmGrid.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConvertTimestampsToUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Timestamps used to be written with DateTime.Now on a server running in India
            // Standard Time (UTC+05:30). They are now stored in UTC. TransportTrips.DispatchDate
            // is a calendar date, not an instant, and is left unchanged.
            migrationBuilder.Sql("UPDATE AspNetUsers SET CreatedAt = DATEADD(minute, -330, CreatedAt);");
            migrationBuilder.Sql("UPDATE CartItems SET AddedAt = DATEADD(minute, -330, AddedAt);");
            migrationBuilder.Sql("UPDATE Orders SET CreatedAt = DATEADD(minute, -330, CreatedAt);");
            migrationBuilder.Sql("UPDATE Products SET CreatedAt = DATEADD(minute, -330, CreatedAt);");
            migrationBuilder.Sql("UPDATE QuickSellListings SET CreatedAt = DATEADD(minute, -330, CreatedAt);");
            migrationBuilder.Sql("UPDATE QuickSellListings SET ExpiresAt = DATEADD(minute, -330, ExpiresAt);");
            migrationBuilder.Sql("UPDATE QuickSellOrders SET PurchasedAt = DATEADD(minute, -330, PurchasedAt);");
            migrationBuilder.Sql("UPDATE TransportParticipants SET JoinedAt = DATEADD(minute, -330, JoinedAt);");
            migrationBuilder.Sql("UPDATE TransportTrips SET CreatedAt = DATEADD(minute, -330, CreatedAt);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE AspNetUsers SET CreatedAt = DATEADD(minute, 330, CreatedAt);");
            migrationBuilder.Sql("UPDATE CartItems SET AddedAt = DATEADD(minute, 330, AddedAt);");
            migrationBuilder.Sql("UPDATE Orders SET CreatedAt = DATEADD(minute, 330, CreatedAt);");
            migrationBuilder.Sql("UPDATE Products SET CreatedAt = DATEADD(minute, 330, CreatedAt);");
            migrationBuilder.Sql("UPDATE QuickSellListings SET CreatedAt = DATEADD(minute, 330, CreatedAt);");
            migrationBuilder.Sql("UPDATE QuickSellListings SET ExpiresAt = DATEADD(minute, 330, ExpiresAt);");
            migrationBuilder.Sql("UPDATE QuickSellOrders SET PurchasedAt = DATEADD(minute, 330, PurchasedAt);");
            migrationBuilder.Sql("UPDATE TransportParticipants SET JoinedAt = DATEADD(minute, 330, JoinedAt);");
            migrationBuilder.Sql("UPDATE TransportTrips SET CreatedAt = DATEADD(minute, 330, CreatedAt);");
        }
    }
}
