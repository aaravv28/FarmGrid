using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmGrid.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReactivateSoldOutListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IsActive used to be cleared automatically on sell-out; it now means only
            // "deleted by the farmer". Undo the automatic clears so restocking works.

            // A product inactive at zero stock was almost certainly sold out, not deleted.
            // It stays hidden (no stock) until the farmer restocks it.
            migrationBuilder.Sql("UPDATE Products SET IsActive = 1 WHERE IsActive = 0 AND StockQuantity <= 0;");

            // Quick Sell lots had no delete action, so IsActive = 0 only ever meant sold out.
            migrationBuilder.Sql("UPDATE QuickSellListings SET IsActive = 1 WHERE IsActive = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only migration; the previous automatic deactivation is not restored.
        }
    }
}
