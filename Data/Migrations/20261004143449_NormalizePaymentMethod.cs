using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmGrid.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizePaymentMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cash on Delivery is the only payment method. "Online Payment" never
            // reached a gateway, and Quick Sell stored a longer COD label.
            migrationBuilder.Sql("UPDATE Orders SET PaymentMethod = 'Cash on Delivery' WHERE PaymentMethod <> 'Cash on Delivery';");
            migrationBuilder.Sql("UPDATE QuickSellOrders SET PaymentMethod = 'Cash on Delivery' WHERE PaymentMethod <> 'Cash on Delivery';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only migration; the previous labels are not restored.
        }
    }
}
