using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmGrid.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameQuickSellBuyerToCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BuyerPhone",
                table: "QuickSellOrders",
                newName: "PhoneNumber");

            migrationBuilder.RenameColumn(
                name: "BuyerName",
                table: "QuickSellOrders",
                newName: "CustomerName");

            migrationBuilder.RenameColumn(
                name: "BuyerId",
                table: "QuickSellOrders",
                newName: "CustomerId");

            migrationBuilder.RenameColumn(
                name: "BuyerEmail",
                table: "QuickSellOrders",
                newName: "CustomerEmail");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PhoneNumber",
                table: "QuickSellOrders",
                newName: "BuyerPhone");

            migrationBuilder.RenameColumn(
                name: "CustomerName",
                table: "QuickSellOrders",
                newName: "BuyerName");

            migrationBuilder.RenameColumn(
                name: "CustomerId",
                table: "QuickSellOrders",
                newName: "BuyerId");

            migrationBuilder.RenameColumn(
                name: "CustomerEmail",
                table: "QuickSellOrders",
                newName: "BuyerEmail");
        }
    }
}
