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
            // is a calendar date, not an instant, and is left unchanged. Placeholder values
            // (0001-01-01, given to rows created before a column existed) are not real times
            // and are skipped; shifting them would overflow.
            migrationBuilder.Sql("UPDATE AspNetUsers SET CreatedAt = DATEADD(minute, -330, CreatedAt) WHERE CreatedAt >= '0001-01-02';");
            migrationBuilder.Sql("UPDATE CartItems SET AddedAt = DATEADD(minute, -330, AddedAt) WHERE AddedAt >= '0001-01-02';");
            migrationBuilder.Sql("UPDATE Orders SET CreatedAt = DATEADD(minute, -330, CreatedAt) WHERE CreatedAt >= '0001-01-02';");
            migrationBuilder.Sql("UPDATE Products SET CreatedAt = DATEADD(minute, -330, CreatedAt) WHERE CreatedAt >= '0001-01-02';");
            migrationBuilder.Sql("UPDATE QuickSellListings SET CreatedAt = DATEADD(minute, -330, CreatedAt) WHERE CreatedAt >= '0001-01-02';");
            migrationBuilder.Sql("UPDATE QuickSellListings SET ExpiresAt = DATEADD(minute, -330, ExpiresAt) WHERE ExpiresAt >= '0001-01-02';");
            migrationBuilder.Sql("UPDATE QuickSellOrders SET PurchasedAt = DATEADD(minute, -330, PurchasedAt) WHERE PurchasedAt >= '0001-01-02';");
            migrationBuilder.Sql("UPDATE TransportParticipants SET JoinedAt = DATEADD(minute, -330, JoinedAt) WHERE JoinedAt >= '0001-01-02';");
            migrationBuilder.Sql("UPDATE TransportTrips SET CreatedAt = DATEADD(minute, -330, CreatedAt) WHERE CreatedAt >= '0001-01-02';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE AspNetUsers SET CreatedAt = DATEADD(minute, 330, CreatedAt) WHERE CreatedAt >= '0001-01-02' AND CreatedAt <= '9999-12-30';");
            migrationBuilder.Sql("UPDATE CartItems SET AddedAt = DATEADD(minute, 330, AddedAt) WHERE AddedAt >= '0001-01-02' AND AddedAt <= '9999-12-30';");
            migrationBuilder.Sql("UPDATE Orders SET CreatedAt = DATEADD(minute, 330, CreatedAt) WHERE CreatedAt >= '0001-01-02' AND CreatedAt <= '9999-12-30';");
            migrationBuilder.Sql("UPDATE Products SET CreatedAt = DATEADD(minute, 330, CreatedAt) WHERE CreatedAt >= '0001-01-02' AND CreatedAt <= '9999-12-30';");
            migrationBuilder.Sql("UPDATE QuickSellListings SET CreatedAt = DATEADD(minute, 330, CreatedAt) WHERE CreatedAt >= '0001-01-02' AND CreatedAt <= '9999-12-30';");
            migrationBuilder.Sql("UPDATE QuickSellListings SET ExpiresAt = DATEADD(minute, 330, ExpiresAt) WHERE ExpiresAt >= '0001-01-02' AND ExpiresAt <= '9999-12-30';");
            migrationBuilder.Sql("UPDATE QuickSellOrders SET PurchasedAt = DATEADD(minute, 330, PurchasedAt) WHERE PurchasedAt >= '0001-01-02' AND PurchasedAt <= '9999-12-30';");
            migrationBuilder.Sql("UPDATE TransportParticipants SET JoinedAt = DATEADD(minute, 330, JoinedAt) WHERE JoinedAt >= '0001-01-02' AND JoinedAt <= '9999-12-30';");
            migrationBuilder.Sql("UPDATE TransportTrips SET CreatedAt = DATEADD(minute, 330, CreatedAt) WHERE CreatedAt >= '0001-01-02' AND CreatedAt <= '9999-12-30';");
        }
    }
}
