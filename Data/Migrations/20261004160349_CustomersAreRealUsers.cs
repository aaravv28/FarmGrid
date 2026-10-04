using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FarmGrid.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomersAreRealUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Customer ids become foreign keys to real users. Carts of missing users are
            // dropped; their orders (history) are adopted by the demo customer, or else the
            // earliest Customer. With orphaned orders and no Customer at all, stop clearly.
            migrationBuilder.Sql("""
                DELETE FROM CartItems WHERE CustomerId NOT IN (SELECT Id FROM AspNetUsers);

                DECLARE @adopter nvarchar(450) = (
                    SELECT TOP 1 u.Id
                    FROM AspNetUsers u
                    JOIN AspNetUserRoles ur ON ur.UserId = u.Id
                    JOIN AspNetRoles r ON r.Id = ur.RoleId AND r.Name = 'Customer'
                    ORDER BY CASE WHEN u.NormalizedEmail = 'CUSTOMER@FARMGRID.COM' THEN 0 ELSE 1 END, u.CreatedAt);

                IF @adopter IS NULL AND (
                       EXISTS (SELECT 1 FROM Orders WHERE CustomerId NOT IN (SELECT Id FROM AspNetUsers))
                    OR EXISTS (SELECT 1 FROM QuickSellOrders WHERE CustomerId NOT IN (SELECT Id FROM AspNetUsers)))
                    THROW 50000, 'Some orders belong to customer ids that are not users, and no Customer user exists to adopt them. Create a Customer account, then run the migration again.', 1;

                UPDATE Orders SET CustomerId = @adopter WHERE CustomerId NOT IN (SELECT Id FROM AspNetUsers);
                UPDATE QuickSellOrders SET CustomerId = @adopter WHERE CustomerId NOT IN (SELECT Id FROM AspNetUsers);
                """);

            migrationBuilder.AlterColumn<string>(
                name: "CustomerId",
                table: "QuickSellOrders",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "CustomerId",
                table: "Orders",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_QuickSellOrders_CustomerId",
                table: "QuickSellOrders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId",
                table: "Orders",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_AspNetUsers_CustomerId",
                table: "CartItems",
                column: "CustomerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_AspNetUsers_CustomerId",
                table: "Orders",
                column: "CustomerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuickSellOrders_AspNetUsers_CustomerId",
                table: "QuickSellOrders",
                column: "CustomerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CartItems_AspNetUsers_CustomerId",
                table: "CartItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_AspNetUsers_CustomerId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_QuickSellOrders_AspNetUsers_CustomerId",
                table: "QuickSellOrders");

            migrationBuilder.DropIndex(
                name: "IX_QuickSellOrders_CustomerId",
                table: "QuickSellOrders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CustomerId",
                table: "Orders");

            migrationBuilder.AlterColumn<string>(
                name: "CustomerId",
                table: "QuickSellOrders",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "CustomerId",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
