using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmGrid.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeOrderStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Quick Sell orders used "Confirmed"; both order types now start as "Placed".
            migrationBuilder.Sql("UPDATE QuickSellOrders SET Status = 'Placed' WHERE Status = 'Confirmed';");

            // One-off cleanup: orders from before checkout split by farmer may mix farmers
            // and have no owning Farmer to move them on, so they are closed as Delivered.
            migrationBuilder.Sql("UPDATE Orders SET Status = 'Delivered' WHERE FarmerId IS NULL AND Status = 'Placed';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only migration; previous statuses are not restored.
        }
    }
}
