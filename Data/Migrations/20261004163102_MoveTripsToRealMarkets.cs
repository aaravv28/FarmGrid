using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmGrid.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveTripsToRealMarkets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Trips go to real Gujarat APMC markets (Models/Markets.cs) instead of three
            // generic names; existing trips move to the matching real market.
            migrationBuilder.Sql("UPDATE TransportTrips SET DestinationMarket = 'Anand APMC' WHERE DestinationMarket = 'Central Mandi';");
            migrationBuilder.Sql("UPDATE TransportTrips SET DestinationMarket = 'Vadodara APMC' WHERE DestinationMarket = 'City Hub';");
            migrationBuilder.Sql("UPDATE TransportTrips SET DestinationMarket = 'Ahmedabad APMC' WHERE DestinationMarket = 'Wholesale Market';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE TransportTrips SET DestinationMarket = 'Central Mandi' WHERE DestinationMarket = 'Anand APMC';");
            migrationBuilder.Sql("UPDATE TransportTrips SET DestinationMarket = 'City Hub' WHERE DestinationMarket = 'Vadodara APMC';");
            migrationBuilder.Sql("UPDATE TransportTrips SET DestinationMarket = 'Wholesale Market' WHERE DestinationMarket = 'Ahmedabad APMC';");
        }
    }
}
